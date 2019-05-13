using System;
using System.Cli;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using NLyric.Settings;

namespace NLyric {
	public static class Program {
		private static void Main(string[] args) {
			if (args == null || args.Length == 0)
				return;

			CliArguments arguments;

			try {
				Console.Title = GetTitle();
			}
			catch {
			}
			if (!CommandLine.TryParse(args, out arguments))
				throw new ArgumentException("命令行参数有误");
			AllSettings.Default = JsonConvert.DeserializeObject<AllSettings>(File.ReadAllText("Settings.json"));
			CliWorker.ExecuteAsync(arguments).GetAwaiter().GetResult();
			Logger.Instance.LogInfo("完成，请按任意键退出...");
			Console.ReadKey(true);
		}

		private static string GetTitle() {
			string productName;
			string version;
			string copyright;
			int firstBlankIndex;
			string copyrightOwnerName;
			string copyrightYear;

			productName = GetAssemblyAttribute<AssemblyProductAttribute>().Product;
			version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
			copyright = GetAssemblyAttribute<AssemblyCopyrightAttribute>().Copyright.Substring(12);
			firstBlankIndex = copyright.IndexOf(' ');
			copyrightOwnerName = copyright.Substring(firstBlankIndex + 1);
			copyrightYear = copyright.Substring(0, firstBlankIndex);
			return $"{productName} v{version} by {copyrightOwnerName} {copyrightYear}";
		}

		private static T GetAssemblyAttribute<T>() {
			return (T)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(T), false)[0];
		}
	}
}
