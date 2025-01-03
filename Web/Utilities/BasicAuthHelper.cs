using System.Net.Http.Headers;
using System.Text;

namespace Web.Utilities;

public class BasicAuthHelper
{
    public static AuthenticationHeaderValue GetBasicAuthHeader(string username, string password)
    {
        var byteArray = Encoding.ASCII.GetBytes($"{username}:{password}");
        return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
    }
}
