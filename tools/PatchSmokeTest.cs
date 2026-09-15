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

    private static List<ReplacePattern> CurrentPatterns(Bag bag, string appName, string fileName)
    {
        return bag.Apps[appName].FileCommonModifyInfos[fileName][0].ReplacePatterns;
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

        List<Change> allChanges = ModifyFinder.FindChanges(testFile, patterns);
        Assert(allChanges.Count == patterns.Count, label + " did not uniquely match every original signature");
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
        Assert(remainingChanges.Count == patterns.Count - firstCategory.Count,
            label + " mixed-state installation did not return only missing features");
        FileUtil.EditMultiHex(testFile, remainingChanges);
        installed = ModifyFinder.FindReplacedFunction(testFile, patterns);
        Assert(categories.All(installed.Contains), label + " mixed-state completion failed");

        byte[] original = File.ReadAllBytes(source);
        File.Copy(source, testFile, true);
        Assert(original.SequenceEqual(File.ReadAllBytes(testFile)), label + " copy/restore round trip changed bytes");
        Console.WriteLine("PASS " + label + ": " + string.Join(", ", categories));
    }

    private static int Main(string[] args)
    {
        try
        {
            bool metadataOnly = args.Length == 2 && args[0] == "--metadata-only";
            if (!metadataOnly && args.Length != 4)
            {
                Console.Error.WriteLine("usage: PatchSmokeTest <patch.json> <Weixin.dll original> <wrapper.node original> <work-dir>");
                Console.Error.WriteLine("   or: PatchSmokeTest --metadata-only <patch.json>");
                return 2;
            }

            string patchJsonPath = metadataOnly ? args[1] : args[0];
            Bag bag = JsonConvert.DeserializeObject<Bag>(File.ReadAllText(patchJsonPath, Encoding.UTF8));
            Assert(bag != null && bag.LatestVersion == "3.0" && bag.PatchVersion == 20260915,
                "unexpected 3.0 patch data version");

            Assembly assembly = typeof(Bag).Assembly;
            Assert(assembly.GetName().Version.ToString() == "3.0.0.0", "unexpected assembly version");
            Assert(((AssemblyCompanyAttribute)assembly.GetCustomAttributes(typeof(AssemblyCompanyAttribute), false).Single()).Company == "Yanzhen Guo",
                "unexpected assembly maintainer");

            FieldInfo urlsField = typeof(HttpUtil).GetField("urls", BindingFlags.NonPublic | BindingFlags.Static);
            string[] updateUrls = (string[])urlsField.GetValue(null);
            Assert(updateUrls.Length > 0 && updateUrls.All(url => url.Contains("yzguo/RevokeMsgPatcher")),
                "update source still points outside the maintained repository");

            byte[] mainAssembly = File.ReadAllBytes(assembly.Location);
            Assert(!ContainsBytes(mainAssembly, Encoding.UTF8.GetBytes("www.google-analytics.com")),
                "Google Analytics endpoint remains in the main executable");

            List<ReplacePattern> weixin = CurrentPatterns(bag, "Weixin", "Weixin.dll");
            List<ReplacePattern> qqnt = CurrentPatterns(bag, "QQNT", "wrapper.node");
            Assert(qqnt.Select(p => p.Category).Distinct().OrderBy(x => x)
                .SequenceEqual(new[] { "私聊防撤回", "群聊防撤回" }.OrderBy(x => x)),
                "QQNT group/private UI categories are missing");

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

            Directory.CreateDirectory(args[3]);
            TestPatchSet("Weixin", args[1], weixin, args[3]);
            TestPatchSet("QQNT", args[2], qqnt, args[3]);
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
