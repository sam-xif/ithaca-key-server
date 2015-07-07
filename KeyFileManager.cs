using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using IthacaKeyServer.Config;

namespace IthacaKeyServer
{
    public class KeyFileManager
    {
        private Configuration m_config;
        private Hashtable m_keyHash;

        public KeyFileManager(Configuration config)
        {
            this.m_config = config;
        }

        public void CreateKeyHashtable(string file)
        {
            Hashtable table = new Hashtable();
            using (FileStream fs = File.OpenRead(file))
            {
                using (StreamReader sr = new StreamReader(fs))
                {
                    string line = sr.ReadLine();
                    string[] keydata = line.Split('\t');

                    // Rely on the fact the all keys are (supposed to be) unique
                    table.Add(keydata[0], keydata[1]);
                }
            }

            this.m_keyHash = table;
        }


        // Returns the key description if the key is found, and string.Empty if otherwise
        public string GetKeyDesc(string key)
        {
            if (m_keyHash.ContainsKey(key))
            {
                return (string)m_keyHash[key];
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
