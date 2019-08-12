using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using ZRCCMangerService.Attributes;
using ZRCCMangerService.BLL;
using ZRCCMangerService.Models;
using ZRCCMangerService.Models.Properties;
using ZRCCMangerService.Utils;

namespace ZRCCMangerService.Controllers
{

    /// <summary>
    /// 员工信息文件导入
    /// </summary>
    public class EmployeeFileController : ApiController
    {
        /// <summary>
        /// 根据文件批量导入员工，文件格式
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        [Logger("员工管理", "员工信息文件导入")]
        //[Token]
        public async Task<MessageCom> Post()
        {

            // Check whether the POST operation is MultiPart?
            if (!Request.Content.IsMimeMultipartContent())
            {
                throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);
            }

            // Prepare CustomMultipartFormDataStreamProvider in which our multipart form
            // data will be loaded.
            string fileSaveLocation = HttpContext.Current.Server.MapPath("~/Data");
            var provider = new CustomMultipartFormDataStreamProvider(fileSaveLocation);
            List<string> files = new List<string>();

            // Read all contents of multipart message into CustomMultipartFormDataStreamProvider.
            await Request.Content.ReadAsMultipartAsync(provider);

            foreach (MultipartFileData file in provider.FileData)
            {
                files.Add(file.LocalFileName);

            }


            foreach (var file in files)
            {
                //Task.Run(() =>
                //{
                var headers = new List<string>();
                var list = ExcelHelper.GetList<Employee>(file,ref headers);

                var pathbak = Path.Combine(Path.GetDirectoryName(file), DateTime.Now.ToString("yyyyMMdd"), Path.GetFileName(file));
                if (File.Exists(pathbak))
                    File.Delete(pathbak);
                Directory.CreateDirectory(Path.GetDirectoryName(pathbak));
                File.Move(file, pathbak);

                var employee_model = new List<string>()
                {
                    "EmployeeNo", "FullName", "IDCardNo", "Photo", "CardNo", "DepartmentId", "Permissions",
                    "GategroupId"
                }; 


                if (headers.Count != 8)
                {
                    return new MessageCom() { IsSuccess = false, Message = "格式错误" };
                }

                foreach (var model in employee_model)
                {
                    if (headers.FirstOrDefault(m=>m.ToLower() == model.ToLower()) ==null )
                    {
                        return new MessageCom() { IsSuccess = false, Message = "格式错误" };
                    }
                }

                var permissions = PermissionBll.Instance.GetList().ToList();
                var groups = GateGroupBll.Instance.GetGateGroups(string.Empty, 1, 1000).ToList();
                var departments = DepartmentBll.Instance.GetAllDepartmentEntitiesNotThree().ToList();

                if (list != null)
                {
                    foreach (var m in list)
                    {
                        m.Permissions = Tool.ConvertNameToID<Permission>(m.Permissions, "PermissionName", permissions);
                        m.GategroupId = Tool.ConvertNameToID<GateGroup>(m.GategroupId, "GroupName", groups);
                        m.DepartmentId =
                            Tool.ConvertNameToID<DepartmentEntity>(m.DepartmentId, "DepartmentName", departments);
                        if (m.DepartmentId.IsNullOrEmpty() || m.IDCardNo.IsNullOrEmpty()  || m.CardNo.IsNullOrEmpty() || m.EmployeeNo.IsNullOrEmpty()|| m.FullName.IsNullOrEmpty()|| m.IDCardNo.Length != 18)
                            return new MessageCom() { IsSuccess = false, Message = m.FullName+ "格式错误" };
                        EmployeeBll.Instance.Add(m);
                        SyncEmployeeBll.Instance.AddEmployeeSyncTasks(m, UpdateType.Insert, true);
                    }
                }

                //});

            }


            return new MessageCom() { IsSuccess = true, Message = "上传成功" };
        }
    }

    // We implement MultipartFormDataStreamProvider to override the filename of File which
    // will be stored on server, or else the default name will be of the format like Body-
    // Part_{GUID}. In the following implementation we simply get the FileName from 
    // ContentDisposition Header of the Request Body.
    public class CustomMultipartFormDataStreamProvider : MultipartFormDataStreamProvider
    {
        public CustomMultipartFormDataStreamProvider(string path) : base(path) { }

        public override string GetLocalFileName(HttpContentHeaders headers)
        {
            return headers.ContentDisposition.FileName.Replace("\"", string.Empty);
        }
    }
}
