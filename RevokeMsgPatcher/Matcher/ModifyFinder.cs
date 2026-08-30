using RevokeMsgPatcher.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace RevokeMsgPatcher.Matcher
{
    public class ModifyFinder
    {
        // TODO 该逻辑需要优化！
        public static List<Change> FindChanges(string path, List<ReplacePattern> replacePatterns)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            // 读取整个文件(dll)
            byte[] fileByteArray = File.ReadAllBytes(path);
            Console.WriteLine("读取文件耗时：{0}ms.", sw.Elapsed.TotalMilliseconds);

            List<Change> changes = new List<Change>(); // 匹配且需要替换的地方
            SortedSet<string> alreadyReplaced = new SortedSet<string>();
            List<string> mismatchDetails = new List<string>();

            // 每条特征独立校验。原实现只统计 Search 总命中数，文件中若已有一个
            // 功能而另一个功能尚未安装，就会错误地报告 [1]/[2]。
            foreach (ReplacePattern pattern in replacePatterns)
            {
                if (pattern.Search == null || pattern.Replace == null || pattern.Search.Length != pattern.Replace.Length)
                {
                    mismatchDetails.Add($"{pattern.Category}：特征配置长度无效");
                    continue;
                }

                int[] searchMatchIndexes = FuzzyMatcher.MatchAll(fileByteArray, pattern.Search);
                int[] replaceMatchIndexes = FuzzyMatcher.MatchAll(fileByteArray, pattern.Replace);
                Console.WriteLine("匹配{0}耗时：{1}ms.", pattern.Category, sw.Elapsed.TotalMilliseconds);

                // 未安装：原始特征必须唯一命中，补丁后特征不能命中。
                if (searchMatchIndexes.Length == 1 && replaceMatchIndexes.Length == 0)
                {
                    changes.Add(new Change(searchMatchIndexes[0], pattern.Replace));
                    continue;
                }

                // 已安装：允许和其他尚未安装的功能混合存在，稍后只写缺失部分。
                if (searchMatchIndexes.Length == 0 && replaceMatchIndexes.Length == 1)
                {
                    alreadyReplaced.Add(pattern.Category);
                    continue;
                }

                mismatchDetails.Add($"{pattern.Category}：原始特征命中 {searchMatchIndexes.Length}，补丁特征命中 {replaceMatchIndexes.Length}");
            }

            if (mismatchDetails.Count > 0)
            {
                throw new BusinessException("match_inconformity",
                    "特征比对失败，未对文件进行修改：\n" + string.Join("\n", mismatchDetails) +
                    "\n当前版本可能改变了处理逻辑，请勿强制安装。 ");
            }

            if (changes.Count == 0)
            {
                throw new BusinessException("match_already_replace",
                    $"特征比对：所选功能已经安装！【{string.Join("、", alreadyReplaced)}】");
            }

            return changes;
        }

        public static SortedSet<string> FindReplacedFunction(string path, List<ReplacePattern> replacePatterns)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            byte[] fileByteArray = File.ReadAllBytes(path);
            Console.WriteLine("读取文件耗时：{0}ms.", sw.Elapsed.TotalMilliseconds);
            SortedSet<string> installed = new SortedSet<string>();
            foreach (IGrouping<string, ReplacePattern> categoryPatterns in replacePatterns
                .Where(pattern => !string.IsNullOrEmpty(pattern.Category))
                .GroupBy(pattern => pattern.Category))
            {
                bool allReplaced = categoryPatterns.All(pattern =>
                {
                    int[] searchMatches = FuzzyMatcher.MatchAll(fileByteArray, pattern.Search);
                    int[] replaceMatches = FuzzyMatcher.MatchAll(fileByteArray, pattern.Replace);
                    return searchMatches.Length == 0 && replaceMatches.Length == 1;
                });

                if (allReplaced)
                {
                    installed.Add(categoryPatterns.Key);
                }
            }
            Console.WriteLine("匹配耗时：{0}ms.", sw.Elapsed.TotalMilliseconds);
            return installed;
        }
    }
}
