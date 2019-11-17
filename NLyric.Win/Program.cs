using System;
using System.Windows.Forms;

namespace NLyric.Win {
	internal static class Program {
		[STAThread]
		private static void Main() {
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new MainForm());
		}
	}
}
