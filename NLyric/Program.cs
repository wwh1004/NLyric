using System;
using System.Cli;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLyric.Settings;

namespace NLyric {
	public static class Program {
		private static async Task Main(string[] args) {
			if (args is null || args.Length == 0) {
				CommandLine.ShowUsage<Arguments>();
				return;
			}

			try {
				Console.Title = GetTitle();
			}
			catch {
			}
			if (!CommandLine.TryParse(args, out Arguments arguments)) {
				CommandLine.ShowUsage<Arguments>();
				return;
			}
			AllSettings.Default = JsonConvert.DeserializeObject<AllSettings>(File.ReadAllText("Settings.json"));
			await NLyricImpl.ExecuteAsync(arguments);
			FastConsole.WriteLine("完成", ConsoleColor.Green);
			FastConsole.Synchronize();
			if (Debugger.IsAttached) {
				Console.WriteLine("按任意键继续...");
				try {
					Console.ReadKey(true);
				}
				catch {
				}
			}
		}

		private static string GetTitle() {
			string productName = GetAssemblyAttribute<AssemblyProductAttribute>().Product;
			string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
			string copyright = GetAssemblyAttribute<AssemblyCopyrightAttribute>().Copyright.Substring(12);
			int firstBlankIndex = copyright.IndexOf(' ');
			string copyrightOwnerName = copyright.Substring(firstBlankIndex + 1);
			string copyrightYear = copyright.Substring(0, firstBlankIndex);
			return $"{productName} v{version} by {copyrightOwnerName} {copyrightYear}";
		}

		private static T GetAssemblyAttribute<T>() {
			return (T)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(T), false)[0];
		}
	}
}
