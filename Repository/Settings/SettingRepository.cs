using Components.Common;
using Models.DTOs;
using Models.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repository.Settings
{

    public class SettingRepository : ISettingRepository
    {
        private readonly AppDbContext _context;

        public SettingRepository(AppDbContext context)
        {
            _context = context;
        }

        public Setting GetSettingsKey(string key)
        {
            var setting = _context.Settings
                .FirstOrDefault(x => x.Key == key);

            if (setting != null)
            {
                setting = DecryptedSensitiveKeys(setting);
            }

            return setting;
        }

        private Setting DecryptedSensitiveKeys(Setting data)
        {
            var decryptKey = new Encrypt_BT();

            if (data != null)
            {
                if (data.Key == SystemVariables.AppURL)
                {
                    if (decryptKey.isEncrypted(data.Value))
                    {
                        data.Value = decryptKey.DecryptString(data.Value);
                    }
                }
            }

            return data;
        }
        public string GetSettingsKeyValue(string settingKey)
        {
            var setting = GetSettingsKey(settingKey);
            return setting?.Value;
        }
    }
}
