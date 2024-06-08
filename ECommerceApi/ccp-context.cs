using System;
using Newtonsoft.Json;
using System.Threading;
using Intertech.Esb.Application.Sso;
using Intertech.CallCenterPlusNuget.Dto;
using Intertech.CallCenterPlusNuget.Helper;

namespace Intertech.CallCenterPlusNuget.CommonContext.App
{
    /// <summary>
    /// Context
    /// </summary>
    public class CcpContext
    {
        private static readonly AsyncLocal<CcpContext> current = new();
        private User user;
        private string userCode;
        private string registerId;
        private string requestId;

        /// <summary>
        /// Common meta data
        /// </summary>
        /// <value></value>
        public static CcpContext Current
        {
            get
            {
                return current.Value ??= new CcpContext();
            }
            set
            {
                current.Value = value;
            }
        }

        /// <summary>
        /// Request difference guid
        /// </summary>
        /// <value></value>
        [JsonProperty("RequestId")]
        public string RequestId
        {
            get
            {
                if (string.IsNullOrEmpty(requestId))
                {
                    requestId = Guid.NewGuid().ToString();
                }

                return requestId;
            }
        }

        /// <summary>
        /// Auth token
        /// </summary>
        /// <value></value>
        [JsonProperty("SessionToken")]
        public string SessionToken { get; set; }
        /// <summary>
        /// UserCode
        /// </summary>
        /// <value></value>
        [JsonProperty("UserCode")]
        public string UserCode
        {
            get
            {
                if (string.IsNullOrEmpty(userCode))
                {
                    return User?.UserCode;
                }

                return userCode;
            }
            set
            {
                userCode = value;
            }
        }
        /// <summary>
        /// AuthTokenExtended
        /// </summary>
        /// <value></value>
        [System.Text.Json.Serialization.JsonIgnore]
        public CcpAuthToken AuthToken { get; set; }
        /// <summary>
        /// UserCode
        /// </summary>
        /// <value></value>
        [System.Text.Json.Serialization.JsonIgnore]
        public User User
        {
            get
            {
                if (user == null || user.UserCode != userCode)
                {
                    user = ContextHelper.GetUserSilenty(userCode);
                }

                return user;
            }
        }

        /// <summary>
        /// RegisterId
        /// </summary>
        /// <value></value>
        [System.Text.Json.Serialization.JsonIgnore]
        public string RegisterId
        {
            get
            {
                if (string.IsNullOrEmpty(registerId))
                {
                    registerId = User?.RegisterId;
                }

                return registerId;
            }
        }
    }
}
