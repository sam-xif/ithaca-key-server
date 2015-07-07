using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using IthacaKeyServer.Config;

namespace IthacaKeyServer
{
    public class KeyFileManager : IDisposable
    {
        private FileStream m_keyFile;
        private string m_keyFilePath;
        private Hashtable m_keyHash;

        private bool m_disposed;

        public KeyFileManager(string path)
        {
            m_keyFilePath = path;
        }

        // Builds the hashtable of the KeyFileManager object
        public void BuildKeyHashtable()
        {
            if (m_disposed) throw new ObjectDisposedException("KeyFileManager");

            m_keyFile = File.Open(m_keyFilePath, FileMode.Open, FileAccess.ReadWrite);

            Hashtable table = new Hashtable();
            if (m_keyFile.Length == 0)
            {
                throw new Exception("There are no keys in the key file.");
            }

            m_keyFile.Position = 0;
            StreamReader sr = new StreamReader(m_keyFile);
            while (!sr.EndOfStream)
            {
                string line = sr.ReadLine();
                string[] keydata = line.Split('\t');

                // Rely on the fact the all keys are (supposed to be) unique
                table.Add(keydata[0], keydata[1]);
            }

            // Closes the FileStream too
            sr.Close();

            this.m_keyHash = table;
        }


        // Returns the key description if the key is found, and string.Empty if otherwise
        // TODO: Change this so it returns a Tuple of the key id and the key desc.
        public string GetKeyDesc(string key)
        {
            if (m_disposed) throw new ObjectDisposedException("KeyFileManager");

            if (m_keyHash.ContainsKey(key))
            {
                return (string)m_keyHash[key];
            }
            else
            {
                return string.Empty;
            }
        }

        // Creates num keys, all associated with the specified description, and writes them to the file.
        public void CreateKeys(int num, string desc)
        {
            if (m_disposed) throw new ObjectDisposedException("KeyFileManager");

            m_keyFile = File.Open(m_keyFilePath, FileMode.Append, FileAccess.Write);

            StreamWriter sw = new StreamWriter(m_keyFile);
            for (int i = 0; i < num; i++)
            {
                // 16 is the default key length
                string key = KeyGenerator.GenerateOne(16);
                sw.WriteLine(key + "\t" + desc);
            }
            
            // Closes the FileStream too
            sw.Flush();
            sw.Close();
        }

        public void Close()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (!m_disposed)
            {
                m_keyFile.Close();
                m_disposed = true;
            }
        }
    }
}
