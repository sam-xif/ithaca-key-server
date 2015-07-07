using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace IthacaKeyServer
{
    public static class KeyGenerator
    {
        // Generates num number of keys and stores them in a string array.
        public static string[] Generate(int num, int length)
        {
            string[] ret = new string[num];
            for (int i = 0; i < num; i++)
            {
                ret[i] = GenerateOne(length);
            }
            return ret;
        }

        public static string GenerateOne(int length)
        {
            return Guid.NewGuid().ToString("n").Substring(0, length);
        }
    }
}
