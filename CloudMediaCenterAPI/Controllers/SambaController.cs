using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CloudMediaCenterAPI.Dtos.In;
using CloudMediaCenterAPI.Dtos.Out;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Extensions;
using SMBLibrary;
using SMBLibrary.Client;

namespace CloudMediaCenterAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SambaController : ControllerBase
    {
        private readonly ILogger<SambaController> _logger;

        public SambaController(ILogger<SambaController> logger)
        {
            _logger = logger;
        }

        [HttpGet("shares")]
        public async Task<IActionResult> GetListOfShareElements([FromQuery] GetAllSharesParams configParams)
        {
            var shareItems = new List<ShareItem>();
            var client = new SMB2Client();
            var isConnected = client.Connect(IPAddress.Parse("192.168.0.185"), SMBTransportType.DirectTCPTransport);
            if (!isConnected)
            {
                return new UnauthorizedResult();
            }
            
            var status = client.Login(string.Empty, configParams.Email, configParams.Password);
            if (status == NTStatus.STATUS_SUCCESS)
            {
                var shares = client.ListShares(out status);

                shareItems.AddRange(shares.Select(share => new ShareItem { ShareName = share }));

                client.Logoff();
            }
            else
            {
                return new UnauthorizedResult();
            }
            
            client.Disconnect();
            return Ok(shareItems);
        }
        
        
        [HttpGet("files")]
        public async Task<IActionResult> GetListOfFilesElements([FromQuery] GetShareContentParams configParams)
        {
            var files = new ShareContent
            {
                ShareName = configParams.ShareName,
                ShareContentListItems = new List<ShareContentListItem>()
            };

            var items = new List<ShareContentListItem>();
            
            var client = new SMB2Client();
            var isConnected = client.Connect(IPAddress.Parse("192.168.0.185"), SMBTransportType.DirectTCPTransport);
            if (!isConnected)
            {
                return new UnauthorizedResult();
            }
            
            var status = client.Login(string.Empty, configParams.Email, configParams.Password);
            if (status != NTStatus.STATUS_SUCCESS)
            {
                return new UnauthorizedResult();
            }
            
            var fileStore = client.TreeConnect(configParams.ShareName, out status);

            status = fileStore.CreateFile(out var directoryHandle, out var fileStatus, string.Empty, AccessMask.GENERIC_READ, FileAttributes.Directory, ShareAccess.Read | ShareAccess.Write, CreateDisposition.FILE_OPEN, CreateOptions.FILE_DIRECTORY_FILE, null);
                
            if (status == NTStatus.STATUS_SUCCESS)
            {
                status = fileStore.QueryDirectory(out var fileList, directoryHandle, "*", FileInformationClass.FileDirectoryInformation);
                status = fileStore.CloseFile(directoryHandle);

                items.AddRange(fileList.Cast<FileDirectoryInformation>().Select(file => new ShareContentListItem { FileName = file.FileName, FileSize = file.AllocationSize / 1024, CreatedAt = file.CreationTime, LastModifiedAt = file.LastWriteTime }));

                files.ShareContentListItems = items;
            }
                
            status = fileStore.Disconnect();

            if (!files.ShareContentListItems.Any())
            {
                return new UnauthorizedResult();
            }

            return Ok(files);
        }
        
        [HttpGet("download")]
        public async Task<IActionResult> DownloadFilesElements([FromQuery] DownloadFilesParams configParams)
        {
            var extension = configParams.FileName.Split(".")[1];
            var client = new SMB2Client();
            var isConnected = client.Connect(IPAddress.Parse("192.168.0.185"), SMBTransportType.DirectTCPTransport);
            if (!isConnected)
            {
                return new UnauthorizedResult();
            }
            
            var status = client.Login(string.Empty, configParams.Email, configParams.Password);
            
            if (status != NTStatus.STATUS_SUCCESS)
            {
                return new UnauthorizedResult();
            }
            
            
            var fileStore = client.TreeConnect(configParams.ShareName, out status);
            var filePath = configParams.FileName;

            status = fileStore.CreateFile(out var fileHandle, out var fileStatus, filePath, AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE, FileAttributes.Normal, ShareAccess.Read, CreateDisposition.FILE_OPEN, CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT, null);

            if (status != NTStatus.STATUS_SUCCESS)
            {
                return new NotFoundResult();
            }
            
            const long bytesRead = 0;

            status = fileStore.ReadFile(out var data, fileHandle, bytesRead, (int)client.MaxReadSize);
            status = fileStore.CloseFile(fileHandle);
            status = fileStore.Disconnect();
            
            Response.Headers.Add("content-disposition", $"attachment; filename={configParams.FileName}");

            switch (extension)
            {
                case "svg":
                    Response.Headers.Add("content-type", "image/svg+xml");
                    break;
                case "mp4":
                    Response.Headers.Add("content-type", "audio/mp4");
                    break;
                case "avi":
                    Response.Headers.Add("content-type", "video/x-msvideo");
                    break;
                case "mpeg":
                    Response.Headers.Add("content-type", "video/mpeg");
                    break;
                case "mp3":
                    Response.Headers.Add("content-type", "audio/mpeg");
                    break;
                default:
                    Response.Headers.Add("content-type", "text/plain");
                    break;
            }

            return extension switch
            {
                "svg" => File(data, ""),
                "mp4" => File(data, "audio/mp4"),
                "avi" => File(data, "video/x-msvideo"),
                "mpeg" => File(data, "video/mpeg"),
                "mp3" => File(data, "audio/mpeg"),
                _ => File(data, "text/plain")
            };
        }
    }
}