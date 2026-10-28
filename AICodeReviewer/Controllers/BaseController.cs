using Microsoft.AspNetCore.Mvc;
using Models.Entities;
using Newtonsoft.Json;
using System.Security.Claims;

namespace AICodeReviewer.Controllers
{
    public class BaseController : Controller
    {
        #region Private Variables and properties
        #endregion

        #region Constructor
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="args"></param>
        //public BaseController(ControllerArgument args)
        //{
        //    _args = args;
        //}
        #endregion

        #region GetClaim
        /// <summary>
        /// GetClaim
        /// </summary>
        /// <param name="claimtype"></param>
        /// <returns></returns>
        protected Claim GetClaim(string claimtype) => (User.Identity as ClaimsIdentity).FindFirst(claimtype);
        /// <summary>
        /// Get User id from claim
        /// </summary>
        /// <returns></returns>
        protected long _userId =>long.Parse(GetClaim("userId").Value);
        protected string _fullName => GetClaim(ClaimTypes.Name).Value;
        protected int _roleId => int.Parse(GetClaim(ClaimTypes.Role).Value);

        protected long _roleType => long.Parse(GetClaim("RoleType").Value);
        protected long _OrganizationId => long.Parse(GetClaim("orgId").Value);
        /// <summary>
        /// Get group id from claim
        /// </summary>
        /// <returns></returns>
        protected long GroupId() => long.Parse(GetClaim(ClaimTypes.GroupSid).Value);
        #endregion

        #region Json serailization
        /// <summary>
        /// Json serailization
        /// </summary>
        /// <param name="value">Passing object</param>
        /// <returns></returns>
        protected string JsonSerializeObject(object value)
        {
            var settings = new JsonSerializerSettings()
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };
            return JsonConvert.SerializeObject(value, settings);
        }
        #endregion
    }
}
