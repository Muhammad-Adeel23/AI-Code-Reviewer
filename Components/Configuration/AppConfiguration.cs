using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration.Json;

namespace Components.Configuration
{
    public class AppConfiguration
    {
        #region Private Variables
        public static IConfigurationRoot _config = null;
        public static long _archiverProcessTime = 2L;
        public static bool _isDevelopmentMode = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" ? true : false;
        public static string _location = $"{Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)}";
        public static string _environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" ? "Development" : "Production";
        //public static string _appConfigPath = $"{Environment.CurrentDirectory}\\appsettings.{_environmentName}.json";
        public static string _appConfigPath = _isDevelopmentMode ? $"{Environment.CurrentDirectory}\\appsettings.{_environmentName}.json" :
                                                                        $"{Path.GetFullPath(Path.Combine(_location, @"..\..\"))}_config\\appsettings.json";
        public static string _dBConnections = string.Empty;

        public static string _appConfigEnvironment = string.Empty;
        public static string _storageServiceUrl = string.Empty;
        public static string _controllerServiceUrl = string.Empty;
        public static string _certificatePath = string.Empty;
        public static string _certificatePassword = string.Empty;

        public static int _tokenDurationInMinutes = 0;
        public static string _domain = string.Empty;
        public static string _secret = string.Empty;
        public static string _validIssuer = string.Empty;
        public static string _clientId = string.Empty;
        public static string _validAudience = string.Empty;

        public static IConfigurationSection _kestrelManagment;

        public static int _eDilligenceSeviceKestrelHttpPort { get; set; }
        public static int _eDilligenceSeviceKestrelHttpsPort { get; set; }
        public static int _storageServiceKestrelHttpPort { get; set; }
        public static int _storageServiceKestrelHttpsPort { get; set; }
        public static int _controllerServiceKestrelHttpPort { get; set; }
        public static int _controllerServiceKestrelHttpsPort { get; set; }

        public static long _fileSizeLimit;

        #endregion

        /// <summary>
        /// Load default configuration settings in app.setting json
        /// </summary>
        /// <returns></returns>
        public static IConfigurationRoot GetConfiguration()
        {
            try
            {
                if (_config == null)
                {
                    _config = new ConfigurationBuilder().AddJsonFile(_appConfigPath).Build();
                }
                GetDBConnectionString();
                return _config;
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        /// <summary>
        /// Read database connection string from appsetting file.
        /// </summary>
        /// <returns></returns>
        public static void GetDBConnectionString()
        {
            try
            {
                if (_config == null)
                {
                    _config = new ConfigurationBuilder().AddJsonFile(_appConfigPath).Build();
                }
                _environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development" ? "Development" : "Production";
                _dBConnections = _config.GetSection("ConnectionStrings:eDilligenceConnectionString").Value;

                _tokenDurationInMinutes = Convert.ToInt32(_config.GetSection("App_Keys:TokenDurationInMinutes").Value);
                _secret = Convert.ToString(_config.GetSection("App_Keys:Secret").Value);
                _validIssuer = Convert.ToString(_config.GetSection("App_Keys:ValidIssuer").Value);
                _validAudience = Convert.ToString(_config.GetSection("App_Keys:ValidAudience").Value);
                _domain = Convert.ToString(_config.GetSection("App_Keys:Domain").Value);
                _clientId = Convert.ToString(_config.GetSection("App_Keys:ClientId").Value);
                _archiverProcessTime = Convert.ToInt32(_config.GetSection("App_Keys:ArchiverProcessTimeInMinute").Value);
                //string fileSizeLimitConfigValue = _config.GetSection("App_Keys:FileSizeLimit").Value;
                //int.TryParse(fileSizeLimitConfigValue, out int fileSizeLimit);
                _fileSizeLimit = 0;// (fileSizeLimit > 0) ? fileSizeLimit : 1000000000;//1GB

                #region KestrelConfiguration
                _kestrelManagment = _config.GetSection("Kestrel");
                _eDilligenceSeviceKestrelHttpPort = GetPort(_config["Kestrel:eDilligence:Http:Url"].ToString());
                _eDilligenceSeviceKestrelHttpsPort = GetPort(_config["Kestrel:eDilligence:HttpsInlineCertFile:Url"].ToString());
                _certificatePath = _config["Kestrel:eDilligence:HttpsInlineCertFile:Certificate:Path"].ToString();
                _certificatePassword = _config["Kestrel:eDilligence:HttpsInlineCertFile:Certificate:Password"].ToString();
                #endregion
            }
            catch (Exception)
            {
                throw;
            }
        }


        public static int GetPort(string url)
        {
            return Convert.ToInt32(url.Split(':').Last());
        }
    }
}
