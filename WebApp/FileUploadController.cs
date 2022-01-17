using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Text.Json;
using System.IO;

namespace webApp
{
    public class FileUploadController : ApiController
    {
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

        public async Task<HttpResponseMessage> PostFile()
        {
            var parameters = HttpContext.Current.Request.Params["fileUploadInfo"];
            UploadFileRequestInfoJson uploadInfo = JsonSerializer.Deserialize<UploadFileRequestInfoJson>(parameters);


            HttpRequestMessage request = this.Request;
            if (!request.Content.IsMimeMultipartContent())
            {
                throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);
            }

            string root = System.IO.Directory.GetCurrentDirectory();
            var provider = new MultipartFormDataStreamProvider(root);

            try
            {
                
                // Read the form data.
                await Request.Content.ReadAsMultipartAsync(provider);

                string destinationPath = "";

                List<string> fileNames = new List<string>();

                int idx = 0;
                foreach (var file in provider.FileData)
                {
                    var sourcePath = Path.GetFileName(file.LocalFileName);
                    destinationPath = Path.Combine(uploadInfo.session, uploadInfo.fileNames[idx]);
                    if (File.Exists(destinationPath))
                    {
                        File.Delete(destinationPath);
                    }
                    File.Move(sourcePath, destinationPath);
                    fileNames.Add(uploadInfo.fileNames[idx]);
                    idx++;
                }

                // TO-DO: destination path is taken only from last file - but it should be only one file!!!

                return Request.CreateResponse<string>(HttpStatusCode.OK, JsonSerializer.Serialize(new RESTFileUploadAnswer(fileNames)));
            }
            catch (System.Exception e)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, e);
            }
        }

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