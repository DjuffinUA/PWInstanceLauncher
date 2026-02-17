namespace PWInstanceLauncher.Services
{
    internal interface ICredentialService
    {
        string Decrypt(string protectedData);
    }
}
