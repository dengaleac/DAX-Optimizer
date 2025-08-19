using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;


namespace DAX_Optimizer.Utilities
{
    public static class ConfigUtil
    {
        private static IConfigurationRoot _configuration;

        public static string GetAPIKey(string strKey)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory()) // or AppContext.BaseDirectory
                .AddJsonFile("./Config/AppSettings.json", optional: false, reloadOnChange: true)
                .Build();

            return config[strKey];
        }
    }
}
