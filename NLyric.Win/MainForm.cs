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
			string productName = GetAssemblyAttribute<AssemblyProductAttribute>(assembly).Product;
			string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
			string copyright = GetAssemblyAttribute<AssemblyCopyrightAttribute>(assembly).Copyright.Substring(12);
			int firstBlankIndex = copyright.IndexOf(' ');
			string copyrightOwnerName = copyright.Substring(firstBlankIndex + 1);
			string copyrightYear = copyright.Substring(0, firstBlankIndex);
			return $"{productName} v{version} by {copyrightOwnerName} {copyrightYear}";
		}

		private static T GetAssemblyAttribute<T>(Assembly assembly) {
			return (T)assembly.GetCustomAttributes(typeof(T), false)[0];
		}

		private void _btnSetDirectory_Click(object sender, EventArgs e) {
			using (var dialog = new FolderBrowserDialog { ShowNewFolderButton = false }) {
				if (dialog.ShowDialog() != DialogResult.OK)
					return;
				_tbDirectory.Text = dialog.SelectedPath;
			}
		}

		private void _cbLogin_CheckedChanged(object sender, EventArgs e) {
			bool state = _cbLogin.Checked;
			_tbAccount.Enabled = state;
			_tbPassword.Enabled = state;
			if (state) {
				if (_tbAccount.Text == "网易云音乐账号")
					_tbAccount.Text = string.Empty;
				if (_tbPassword.Text == "网易云音乐密码")
					_tbPassword.Text = string.Empty;
				_tbPassword.PasswordChar = '*';
			}
		}

		private void _btnRun_Click(object sender, EventArgs e) {
			string arguments = $"-d \"{_tbDirectory.Text}\"";
			if (_cbLogin.Checked)
				arguments += $" -a {_tbAccount.Text} -p {_tbPassword.Text}";
			Process.Start("NLyric.exe", arguments);
		}
	}
}
