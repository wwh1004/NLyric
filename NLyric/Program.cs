using System;
using System.Cli;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using NLyric.Settings;

namespace NLyric {
	public static class Program {
		private static void Main(string[] args) {
			if (args == null || args.Length == 0) {
				CommandLine.ShowUsage<Arguments>();
				return;
			}

			Arguments arguments;

			try {
				Console.Title = GetTitle();
			}
			catch {
			}
			if (!CommandLine.TryParse(args, out arguments)) {
				CommandLine.ShowUsage<Arguments>();
				return;
			}
			AllSettings.Default = JsonConvert.DeserializeObject<AllSettings>(File.ReadAllText("Settings.json"));
			CliWorker.ExecuteAsync(arguments).GetAwaiter().GetResult();
			Logger.Instance.LogInfo("完成", ConsoleColor.Green);
#if DEBUG
			Console.ReadKey(true);
#endif
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
