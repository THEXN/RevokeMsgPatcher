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
        private enum PatternState
        {
            ToInstall,
            Installed,
            Absent, // 布局变体不适用于当前构建
            Invalid
        }

        private class PatternMatch
        {
            public ReplacePattern Pattern;
            public PatternState State;
            public int SearchHits;
            public int ReplaceHits;
            public int Position;
        }

        private static bool HasValidLengths(ReplacePattern pattern)
        {
            return pattern.Search != null && pattern.Replace != null
                && pattern.Search.Length == pattern.Replace.Length;
        }

        private static PatternMatch MatchPattern(byte[] fileByteArray, ReplacePattern pattern)
        {
            // 命中数异常的组合必须保持 Invalid，不能落到枚举默认值上。
            PatternMatch match = new PatternMatch { Pattern = pattern, State = PatternState.Invalid };

            if (!HasValidLengths(pattern))
            {
                return match;
            }

            int[] searchMatchIndexes = FuzzyMatcher.MatchAll(fileByteArray, pattern.Search);
            int[] replaceMatchIndexes = FuzzyMatcher.MatchAll(fileByteArray, pattern.Replace);
            match.SearchHits = searchMatchIndexes.Length;
            match.ReplaceHits = replaceMatchIndexes.Length;

            if (searchMatchIndexes.Length == 1 && replaceMatchIndexes.Length == 0)
            {
                match.State = PatternState.ToInstall;
                match.Position = searchMatchIndexes[0];
            }
            else if (searchMatchIndexes.Length == 0 && replaceMatchIndexes.Length == 1)
            {
                match.State = PatternState.Installed;
            }
            else if (searchMatchIndexes.Length == 0 && replaceMatchIndexes.Length == 0)
            {
                match.State = PatternState.Absent;
            }

            return match;
        }

        // TODO 该逻辑需要优化！
        public static List<Change> FindChanges(string path, List<ReplacePattern> replacePatterns)
        {
            if (replacePatterns == null || replacePatterns.Count == 0)
            {
                throw new BusinessException("match_no_pattern",
                    "未找到所选功能对应的特征，当前版本可能不支持所选功能！");
            }

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
            foreach (ReplacePattern pattern in replacePatterns.Where(p => !p.Alternative))
            {
                if (!HasValidLengths(pattern))
                {
                    mismatchDetails.Add($"{pattern.Category}：特征配置长度无效");
                    continue;
                }

                PatternMatch match = MatchPattern(fileByteArray, pattern);
                Console.WriteLine("匹配{0}耗时：{1}ms.", pattern.Category, sw.Elapsed.TotalMilliseconds);

                // 未安装：原始特征必须唯一命中，补丁后特征不能命中。
                if (match.State == PatternState.ToInstall)
                {
                    changes.Add(new Change(match.Position, pattern.Replace));
                    continue;
                }

                // 已安装：允许和其他尚未安装的功能混合存在，稍后只写缺失部分。
                if (match.State == PatternState.Installed)
                {
                    alreadyReplaced.Add(pattern.Category);
                    continue;
                }

                mismatchDetails.Add($"{pattern.Category}：原始特征命中 {match.SearchHits}，补丁特征命中 {match.ReplaceHits}");
            }

            // 同一功能的布局变体互斥，只有一个变体可以待安装或已安装。
            foreach (IGrouping<string, ReplacePattern> variants in replacePatterns
                .Where(p => p.Alternative && !string.IsNullOrEmpty(p.Category))
                .GroupBy(p => p.Category))
            {
                string category = variants.Key;
                List<ReplacePattern> variantList = variants.ToList();
                if (!variantList.All(HasValidLengths))
                {
                    mismatchDetails.Add($"{category}：布局变体特征配置长度无效");
                    continue;
                }

                List<PatternMatch> matches = variantList
                    .Select(pattern => MatchPattern(fileByteArray, pattern)).ToList();
                Console.WriteLine("匹配{0}变体耗时：{1}ms.", category, sw.Elapsed.TotalMilliseconds);

                int toInstallCount = matches.Count(m => m.State == PatternState.ToInstall);
                int installedCount = matches.Count(m => m.State == PatternState.Installed);

                // 命中数异常的变体说明布局判断不可靠，即使其他变体看起来可用也不能写入。
                if (matches.Any(m => m.State == PatternState.Invalid))
                {
                    mismatchDetails.Add($"{category}：布局变体命中异常，无法确定适用的布局");
                }
                else if (toInstallCount == 1 && installedCount == 0)
                {
                    PatternMatch toInstall = matches.First(m => m.State == PatternState.ToInstall);
                    changes.Add(new Change(toInstall.Position, toInstall.Pattern.Replace));
                }
                else if (installedCount == 1 && toInstallCount == 0)
                {
                    alreadyReplaced.Add(category);
                }
                else if (toInstallCount > 1)
                {
                    mismatchDetails.Add($"{category}：{toInstallCount} 个布局变体同时待安装，无法确定补丁位置");
                }
                else
                {
                    mismatchDetails.Add($"{category}：没有可用的布局变体，请确认文件未被其他补丁修改过");
                }
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
                List<PatternMatch> matches = categoryPatterns
                    .Select(pattern => MatchPattern(fileByteArray, pattern)).ToList();

                // 布局变体互斥：恰好一个变体已安装、其余变体都不适用于当前构建，才算该功能已安装。
                bool installedVariant = categoryPatterns.First().Alternative
                    && matches.All(m => m.State == PatternState.Installed || m.State == PatternState.Absent)
                    && matches.Count(m => m.State == PatternState.Installed) == 1;
                bool allReplaced = installedVariant || (!categoryPatterns.First().Alternative
                    && matches.All(m => m.State == PatternState.Installed));

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
