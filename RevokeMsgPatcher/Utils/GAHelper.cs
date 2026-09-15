using System.Threading.Tasks;

namespace RevokeMsgPatcher.Utils
{
    /// <summary>
    /// 兼容旧调用的无操作统计器。
    /// 3.0 起不再收集或发送使用数据。
    /// </summary>
    public class GAHelper
    {
        private static GAHelper instance;

        public static GAHelper Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new GAHelper();
                }

                return instance;
            }
        }

        public Task RequestPageViewAsync(string page, string title = null)
        {
            return Task.FromResult(0);
        }

        public void RequestPageView(string page, string title = null)
        {
            // Intentionally left blank. Kept to avoid touching unrelated call sites.
        }
    }
}
