using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace FoundationalModel.Services.Helpers
{
    public static class Extension
    {
        public static string ToHexHash(this string content) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    }
}
