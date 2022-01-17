using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.IO;
using System.IO.Compression;

namespace webApp
{
    public class FileFromArchiveDownloadController : ApiController
    {
        public HttpResponseMessage Get(string archiveName, string filePath, string session)
        {
            HttpRequestMessage request = this.Request;
            HttpResponseMessage response;

            List<string> entries = new List<string>();
            MemoryStream ms = new MemoryStream();

            using (ZipArchive archive = ZipFile.OpenRead(Path.Combine(session, archiveName)))
            {
                var unifiedFilePath = filePath.ToUpper().Replace("\\", "/");
                unifiedFilePath = unifiedFilePath.IndexOf("/") == 0 ? unifiedFilePath.Remove(0, 1) : unifiedFilePath;
                ZipArchiveEntry archiveFile = archive.Entries.FirstOrDefault(e => e.FullName.ToUpper().Replace("\\", "/") == unifiedFilePath);
                if (archiveFile != null)
                {
                    archiveFile.Open().CopyTo(ms);
                    ms.Seek(0, SeekOrigin.Begin);

                    response = Request.CreateResponse(HttpStatusCode.OK);
                    response.Content = new StreamContent(ms);
                    response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment");
                    response.Content.Headers.ContentDisposition.FileName = Path.GetFileName(filePath);
                    response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                }
                else
                {
                    response = Request.CreateResponse(HttpStatusCode.NotFound);
                }
            }



            //HttpResponseMessage response = Request.CreateResponse(HttpStatusCode.OK);

            //var dataBytes = File.ReadAllBytes(archiveName);
            //var dataStream = new MemoryStream(dataBytes);
            //response.Content = new StreamContent(dataStream);
            //response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment");
            //response.Content.Headers.ContentDisposition.FileName = Path.GetFileName(archiveName);
            //response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

            return response;
        }

        //// GET api/<controller>
        //public IEnumerable<string> Get()
        //{
        //    return new string[] { "value1", "value2" };
        //}

        //// GET api/<controller>/5
        //public string Get(int id)
        //{
        //    return "value";
        //}

        //// POST api/<controller>
        //public void Post([FromBody] string value)
        //{
        //}

        //// PUT api/<controller>/5
        //public void Put(int id, [FromBody] string value)
        //{
        //}

        //// DELETE api/<controller>/5
        //public void Delete(int id)
        //{
        //}
    }
}