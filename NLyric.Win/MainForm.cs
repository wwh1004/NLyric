using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace NLyric.Win {
	public sealed partial class MainForm : Form {
		public MainForm() {
			InitializeComponent();
			Text = GetTitle(Assembly.Load(File.ReadAllBytes("NLyric.exe")));
			_cbLogin_CheckedChanged(_cbLogin, EventArgs.Empty);
		}

		private static string GetTitle(Assembly assembly) {
			string productName;
			string version;
			string copyright;
			int firstBlankIndex;
			string copyrightOwnerName;
			string copyrightYear;

			productName = GetAssemblyAttribute<AssemblyProductAttribute>(assembly).Product;
			version = assembly.GetName().Version.ToString();
			copyright = GetAssemblyAttribute<AssemblyCopyrightAttribute>(assembly).Copyright.Substring(12);
			firstBlankIndex = copyright.IndexOf(' ');
			copyrightOwnerName = copyright.Substring(firstBlankIndex + 1);
			copyrightYear = copyright.Substring(0, firstBlankIndex);
			return $"{productName} v{version} by {copyrightOwnerName} {copyrightYear}";
		}

		private static T GetAssemblyAttribute<T>(Assembly assembly) {
			return (T)assembly.GetCustomAttributes(typeof(T), false)[0];
		}

		private void _btnSetDirectory_Click(object sender, EventArgs e) {
			using (FolderBrowserDialog dialog = new FolderBrowserDialog { ShowNewFolderButton = false }) {
				if (dialog.ShowDialog() != DialogResult.OK)
					return;
				_tbDirectory.Text = dialog.SelectedPath;
			}
		}

		private void _cbLogin_CheckedChanged(object sender, EventArgs e) {
			bool state;

			state = _cbLogin.Checked;
			_tbAccount.Enabled = state;
			_tbPassword.Enabled = state;
			if (state) {
				if (_tbAccount.Text == "网易云音乐账号")
					_tbAccount.Text = string.Empty;
				if (_tbPassword.Text == "网易云音乐密码")
					_tbPassword.Text = string.Empty;
			}
		}

		private void _btnRun_Click(object sender, EventArgs e) {
			string arguments;

			arguments = $"-d \"{_tbDirectory.Text}\"";
			if (_cbLogin.Checked)
				arguments += $" -a {_tbAccount.Text} -p {_tbPassword.Text}";
			Process.Start("NLyric.exe", arguments);
		}
	}
}
