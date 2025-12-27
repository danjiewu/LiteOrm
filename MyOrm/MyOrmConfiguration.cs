using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Collections;
using System.Collections.Specialized;
using Microsoft.Extensions.Configuration;


namespace MyOrm
{
    public class MyOrmConfiguration
    {
        public void Load(IConfiguration config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            DefaultConnectionName = config["DefaultConnectionName"];
            Connections = config.GetSection("ConnectionStrings").GetChildren().Select(section =>
            {
                var dbconfig = new DbConnectionConfig()
                {
                    Name = section["Name"],
                    ConnectionString = section["ConnectionString"],
                    Provider = section["Provider"]
                };
                if (TimeSpan.TryParse(section["KeepAliveDuration"], out TimeSpan timeSpan)) dbconfig.KeepAliveDuration = timeSpan;
                if (Int32.TryParse(section["PoolSize"], out int poolsize)) dbconfig.PoolSize = poolsize;
                return dbconfig;
            }).ToArray();
        }
        public string DefaultConnectionName { get; set; }
        public DbConnectionConfig[] Connections { get; set; }
    }

    public class DbConnectionConfig
    {
        public string Name { get; set; }
        public string ConnectionString { get; set; }
        public string Provider { get; set; }
        public TimeSpan KeepAliveDuration { get; set; }
        public int PoolSize { get; set; } = 16;
    }
}
