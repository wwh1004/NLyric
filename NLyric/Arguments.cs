using System;
using System.Cli;
using System.IO;

namespace NLyric {
	public sealed class Arguments {
		private string _directory;
		private string _account;
		private string _password;
		private bool _updateOnly;
		private bool _useBatch;

		[Argument("-d", IsRequired = false, DefaultValue = "", Type = "DIR", Description = "存放音乐的文件夹，可以是相对路径或者绝对路径")]
		internal string DirectoryCliSetter {
			set {
				if (string.IsNullOrEmpty(value))
					return;

				Directory = value;
			}
		}

		[Argument("-a", IsRequired = false, DefaultValue = "", Type = "STR", Description = "网易云音乐账号（邮箱/手机号）")]
		internal string AccountCliSetter {
			set {
				if (string.IsNullOrEmpty(value))
					return;

				Account = value;
			}
		}

		[Argument("-p", IsRequired = false, DefaultValue = "", Type = "STR", Description = "网易云音乐密码")]
		internal string PasswordCliSetter {
			set {
				if (string.IsNullOrEmpty(value))
					return;

				Password = value;
			}
		}

		[Argument("--update-only", Description = "仅更新已有歌词")]
		internal bool UpdateOnlyCliSetter {
			set => _updateOnly = value;
		}

		[Argument("--batch", Description = "使用Batch API（实验性）")]
		internal bool UseBatchCliSetter {
			set => _useBatch = value;
		}

		public string Directory {
			get => _directory;
			set {
				if (!System.IO.Directory.Exists(value))
					throw new DirectoryNotFoundException();

				_directory = Path.GetFullPath(value);
			}
		}

		public string Account {
			get => _account;
			set {
				if (string.IsNullOrEmpty(value))
					throw new ArgumentNullException(nameof(value));

				_account = value;
			}
		}

		public string Password {
			get => _password;
			set {
				if (string.IsNullOrEmpty(value))
					throw new ArgumentNullException(nameof(value));

				_password = value;
			}
		}

		public bool UpdateOnly {
			get => _updateOnly;
			set => _updateOnly = value;
		}

		public bool UseBatch {
			get => _useBatch;
			set => _useBatch = value;
		}
	}
}
