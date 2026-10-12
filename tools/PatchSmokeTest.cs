using Newtonsoft.Json;
using RevokeMsgPatcher.Forms;
using RevokeMsgPatcher.Matcher;
using RevokeMsgPatcher.Model;
using RevokeMsgPatcher.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

internal static class PatchSmokeTest
{
    private static int assertions;

    private static void Assert(bool condition, string message)
    {
        assertions++;
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    // 版本区间选择逻辑与 AppModifier.IsInVersionRange 一致，
    // 否则冒烟测试只能在新版本文件上运行。
    private static List<ReplacePattern> CurrentPatterns(Bag bag, string appName, string fileName, string version)
    {
        foreach (CommonModifyInfo info in bag.Apps[appName].FileCommonModifyInfos[fileName])
        {
            if (VersionUtil.Compare(version, info.StartVersion) == 1
                && VersionUtil.Compare(version, info.EndVersion) <= 0)
            {
                return info.ReplacePatterns;
            }
        }

        throw new InvalidOperationException(appName + " " + fileName + " " + version + " 不在任何特征组的版本范围内");
    }

    private static List<ReplacePattern> LatestPatterns(Bag bag, string appName, string fileName)
    {
        return bag.Apps[appName].FileCommonModifyInfos[fileName]
            .OrderByDescending(info => info.StartVersion, Comparer<string>.Create(VersionUtil.Compare))
            .First().ReplacePatterns;
    }

    private static void Apply(string path, List<ReplacePattern> patterns)
    {
        FileUtil.EditMultiHex(path, ModifyFinder.FindChanges(path, patterns));
    }

    private static bool ContainsBytes(byte[] data, byte[] value)
    {
        for (int i = 0; i <= data.Length - value.Length; i++)
        {
            int j = 0;
            while (j < value.Length && data[i + j] == value[j])
            {
                j++;
            }

            if (j == value.Length)
            {
                return true;
            }
        }

        return false;
    }

    private static void TestPatchSet(string label, string source, List<ReplacePattern> patterns, string workDir)
    {
        Assert(File.Exists(source), label + " source does not exist");
        string testFile = Path.Combine(workDir, label + ".bin");
        File.Copy(source, testFile, true);

        string[] categories = patterns.Select(p => p.Category).Distinct().ToArray();
        Assert(ModifyFinder.FindReplacedFunction(testFile, patterns).Count == 0,
            label + " unexpectedly looks patched before the test");

        // 布局变体互斥：每个功能最多只能有一个变体处于待安装状态。
        byte[] original = File.ReadAllBytes(source);
        foreach (IGrouping<string, ReplacePattern> variants in patterns
            .Where(p => p.Alternative).GroupBy(p => p.Category))
        {
            int applicable = variants.Count(p => FuzzyMatcher.MatchAll(original, p.Search).Length == 1);
            Assert(applicable == 1, label + " " + variants.Key + " 有 " + applicable + " 个布局变体命中，应唯一命中");
        }

        // 每个功能解析出一处补丁位置（变体由唯一命中的那个决定）。
        List<Change> allChanges = ModifyFinder.FindChanges(testFile, patterns);
        Assert(allChanges.Count == categories.Length, label + " did not resolve every feature to one patch site");
        FileUtil.EditMultiHex(testFile, allChanges);

        SortedSet<string> installed = ModifyFinder.FindReplacedFunction(testFile, patterns);
        Assert(categories.All(installed.Contains), label + " did not report every installed category");

        bool alreadyInstalled = false;
        try
        {
            ModifyFinder.FindChanges(testFile, patterns);
        }
        catch (ApplicationException ex)
        {
            alreadyInstalled = ex.Message.Contains("已经安装");
        }
        Assert(alreadyInstalled, label + " repeated installation was not safely rejected");

        File.Copy(source, testFile, true);
        List<ReplacePattern> firstCategory = patterns.Where(p => p.Category == categories[0]).ToList();
        Apply(testFile, firstCategory);
        installed = ModifyFinder.FindReplacedFunction(testFile, patterns);
        Assert(installed.SetEquals(new[] { categories[0] }), label + " mixed-state detection is incorrect");

        List<Change> remainingChanges = ModifyFinder.FindChanges(testFile, patterns);
        Assert(remainingChanges.Count == categories.Length - 1,
            label + " mixed-state installation did not return only missing features");
        FileUtil.EditMultiHex(testFile, remainingChanges);
        installed = ModifyFinder.FindReplacedFunction(testFile, patterns);
        Assert(categories.All(installed.Contains), label + " mixed-state completion failed");

        File.Copy(source, testFile, true);
        Assert(original.SequenceEqual(File.ReadAllBytes(testFile)), label + " copy/restore round trip changed bytes");
        Console.WriteLine("PASS " + label + ": " + string.Join(", ", categories));
    }

    private static int Main(string[] args)
    {
        try
        {
            bool metadataOnly = args.Length == 2 && args[0] == "--metadata-only";
            if (!metadataOnly && args.Length < 3)
            {
                Console.Error.WriteLine("usage: PatchSmokeTest <patch.json> <work-dir> [weixin=<原始 Weixin.dll>] [qqnt=<原始 wrapper.node>]...");
                Console.Error.WriteLine("   or: PatchSmokeTest --metadata-only <patch.json>");
                return 2;
            }

            string patchJsonPath = metadataOnly ? args[1] : args[0];
            Bag bag = JsonConvert.DeserializeObject<Bag>(File.ReadAllText(patchJsonPath, Encoding.UTF8));
            Assert(bag != null && bag.LatestVersion == "3.1" && bag.PatchVersion == 20261012,
                "unexpected 3.1 patch data version");

            Assembly assembly = typeof(Bag).Assembly;
            Assert(assembly.GetName().Version.ToString() == "3.1.0.0", "unexpected assembly version");
            Assert(((AssemblyCompanyAttribute)assembly.GetCustomAttributes(typeof(AssemblyCompanyAttribute), false).Single()).Company == "Yanzhen Guo",
                "unexpected assembly maintainer");

            FieldInfo urlsField = typeof(HttpUtil).GetField("urls", BindingFlags.NonPublic | BindingFlags.Static);
            string[] updateUrls = (string[])urlsField.GetValue(null);
            Assert(updateUrls.Length > 0 && updateUrls.All(url => url.Contains("yzguo/RevokeMsgPatcher")),
                "update source still points outside the maintained repository");

            byte[] mainAssembly = File.ReadAllBytes(assembly.Location);
            Assert(!ContainsBytes(mainAssembly, Encoding.UTF8.GetBytes("www.google-analytics.com")),
                "Google Analytics endpoint remains in the main executable");

            // 内嵌资源必须和发布的 patch.json 完全一致，否则离线兜底数据会落后于在线数据。
            PropertyInfo patchJsonProperty = typeof(Bag).Assembly
                .GetType("RevokeMsgPatcher.Properties.Resources", true)
                .GetProperty("PatchJson", BindingFlags.NonPublic | BindingFlags.Static
                    | BindingFlags.Public | BindingFlags.Static);
            string embeddedJson = (string)patchJsonProperty.GetValue(null, null);
            Assert(embeddedJson == File.ReadAllText(patchJsonPath, Encoding.UTF8).Trim(),
                "embedded Resources.PatchJson differs from " + patchJsonPath);

            List<ReplacePattern> qqnt = LatestPatterns(bag, "QQNT", "wrapper.node");
            Assert(qqnt.Select(p => p.Category).Distinct().OrderBy(x => x)
                .SequenceEqual(new[] { "私聊防撤回", "群聊防撤回" }.OrderBy(x => x)),
                "QQNT group/private UI categories are missing");
            Assert(qqnt.Count(p => p.Alternative && p.Category == "私聊防撤回") == 2,
                "QQNT private recall must ship both layout variants");
            Assert(qqnt.Count(p => !p.Alternative) == 1,
                "QQNT group recall pattern must stay single");

            bool emptyPatternsRejected = false;
            try
            {
                ModifyFinder.FindChanges(string.Empty, new List<ReplacePattern>());
            }
            catch (ApplicationException ex)
            {
                emptyPatternsRejected = ex.Message.Contains("未找到所选功能对应的特征");
            }
            Assert(emptyPatternsRejected, "empty feature selection did not return the unsupported-feature error");

            Panel panel = new Panel();
            UIController.AddCategoryCheckBoxToPanel(panel,
                new[] { "群聊防撤回", "私聊防撤回" }, new string[0]);
            Assert(panel.Controls.OfType<CheckBox>().Count() == 2,
                "QQNT UI did not create two feature switches");
            panel.Controls.OfType<CheckBox>().Single(c => c.Text == "私聊防撤回").Checked = false;
            Assert(UIController.GetCategoriesFromPanel(panel).SequenceEqual(new[] { "群聊防撤回" }),
                "QQNT UI switch selection was not preserved");

            if (metadataOnly)
            {
                Console.WriteLine("PASS metadata assertions=" + assertions);
                return 0;
            }

            string workDir = args[1];
            List<string> weixinTargets = new List<string>();
            List<string> qqntTargets = new List<string>();
            foreach (string arg in args.Skip(2))
            {
                if (arg.StartsWith("weixin=", StringComparison.Ordinal))
                {
                    weixinTargets.Add(arg.Substring("weixin=".Length));
                }
                else if (arg.StartsWith("qqnt=", StringComparison.Ordinal))
                {
                    qqntTargets.Add(arg.Substring("qqnt=".Length));
                }
                else
                {
                    Console.Error.WriteLine("unknown target: " + arg);
                    return 2;
                }
            }
            Assert(weixinTargets.Count + qqntTargets.Count > 0, "no binary target given");

            Directory.CreateDirectory(workDir);
            foreach (string weixin in weixinTargets)
            {
                string weixinVersion = FileUtil.GetFileVersion(weixin);
                TestPatchSet("Weixin-" + weixinVersion, weixin,
                    CurrentPatterns(bag, "Weixin", "Weixin.dll", weixinVersion), workDir);
            }
            for (int i = 0; i < qqntTargets.Count; i++)
            {
                // 每个 wrapper.node 对应一种私聊布局，逐一验证互斥变体解析。
                string wrapperVersion = FileUtil.GetFileVersion(qqntTargets[i]);
                TestPatchSet("QQNT" + (i + 1) + "-" + wrapperVersion, qqntTargets[i],
                    CurrentPatterns(bag, "QQNT", "wrapper.node", wrapperVersion), workDir);
            }
            Console.WriteLine("PASS assertions=" + assertions);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL " + ex);
            return 1;
        }
    }
}
