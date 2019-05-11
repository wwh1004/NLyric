using System;
using System.Cli;
using System.IO;
using Newtonsoft.Json;

namespace NLyric {
	internal static class Program {
		private static void Main(string[] args) {
			if (args == null || args.Length == 0)
				return;

			CliArguments arguments;

			if (!CommandLine.TryParse(args, out arguments))
				throw new ArgumentException("命令行参数有误");
			Settings.Default = JsonConvert.DeserializeObject<Settings>(File.ReadAllText("Settings.json"));
			CliWorker.ExecuteAsync(arguments).GetAwaiter().GetResult();
			Logger.Instance.LogInfo("完成，请按任意键退出...");
			Console.ReadKey(true);
		}
	}
}
