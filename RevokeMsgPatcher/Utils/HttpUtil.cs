using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace RevokeMsgPatcher.Utils
{
    public class HttpUtil
    {
        public static HttpClient Client { get; } = new HttpClient();

        static HttpUtil()
        {
            Client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/78.0.3904.108 Safari/537.36");
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
        }

        /// <summary>
        /// 当前维护仓库中的补丁数据目录。
        /// </summary>

        public static string PatchVersion
        {
            get
            {
                string currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
                if (currentVersion.Length > 3)
                {
                    return currentVersion.Substring(0, 3);
                }

                return "3.0";
            }
        }

        private static readonly string[] urls = new string[]
        {
            $"https://raw.githubusercontent.com/yzguo/RevokeMsgPatcher/master/RevokeMsgPatcher.Assistant/Data/{PatchVersion}/patch.json",
            $"https://cdn.jsdelivr.net/gh/yzguo/RevokeMsgPatcher@master/RevokeMsgPatcher.Assistant/Data/{PatchVersion}/patch.json",
        };

        public static async Task<string> GetPatchJsonAsync()
        {
            int i = 0;
            while (i < urls.Length)
            {
                try
                {
                    string json = await Client.GetStringAsync(urls[i]);
                    if (!string.IsNullOrEmpty(json) && json.Contains("LatestVersion"))
                    {
                        return json;
                    }
                    else
                    {
                        Console.WriteLine("第" + (i + 1) + "次请求获得的数据并非期望数据\nURL:" + urls[i]);
                        GAHelper.Instance.RequestPageView($"/main/json/request_ex/{i + 1}/not_my_json", "第" + (i + 1) + "次请求获得的数据并非期望数据");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("第" + (i + 1) + "次请求异常:[" + ex.Message + "]\nURL:" + urls[i]);
                    GAHelper.Instance.RequestPageView($"/main/json/request_ex/{i + 1}/{ex.Message}", "第" + (i + 1) + "次请求异常:[" + ex.Message + "]");
                }
                i++;
            }
            return null;
        }
    }
}
