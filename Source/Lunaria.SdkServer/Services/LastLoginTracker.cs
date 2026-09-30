namespace Lunaria.SdkServer.Services;

public sealed class LastLoginTracker
{
    private string _email = "";

    public string Email => Volatile.Read(ref _email);

    public void Record(string email) => Volatile.Write(ref _email, email);
}
