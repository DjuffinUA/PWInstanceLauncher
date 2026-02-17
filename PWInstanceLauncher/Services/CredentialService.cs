using System.Security.Cryptography;
using System.Text;

namespace PWInstanceLauncher.Services
{
    internal class CredentialService : ICredentialService
    {
        public string Encrypt(string password)
        {
            var bytes = Encoding.UTF8.GetBytes(password);
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        public string Decrypt(string encryptedPassword)
        {
            var bytes = Convert.FromBase64String(encryptedPassword);
            var decrypted = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
    }
}
