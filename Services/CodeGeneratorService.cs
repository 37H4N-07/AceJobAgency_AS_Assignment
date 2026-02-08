using System.Security.Cryptography;

namespace AceJobAgency_AS_Assignment.Services
{
    public class CodeGeneratorService : ICodeGeneratorService
    {
        public string GenerateCode()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] data = new byte[4];
                rng.GetBytes(data);
                int value = Math.Abs(BitConverter.ToInt32(data, 0));
                return (value % 1000000).ToString("D6");
            }
        }
    }
}