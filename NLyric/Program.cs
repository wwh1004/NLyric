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
			await NLyricImpl.ExecuteAsync(arguments);
			FastConsole.WriteLine("完成", ConsoleColor.Green);
			FastConsole.Synchronize();
			if (IsN00bUser() || Debugger.IsAttached) {
				Console.WriteLine("按任意键继续...");
				try {
					Console.ReadKey(true);
				}
				catch {
				}
			}
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

		private static bool IsN00bUser() {
			if (HasEnv("VisualStudioDir"))
				return false;
			if (HasEnv("SHELL"))
				return false;
			return HasEnv("windir") && !HasEnv("PROMPT");
		}

		private static bool HasEnv(string name) {
			foreach (object key in Environment.GetEnvironmentVariables().Keys) {
				string env;

				env = key as string;
				if (env == null)
					continue;
				if (string.Equals(env, name, StringComparison.OrdinalIgnoreCase))
					return true;
			}
			return false;
		}
	}
}
