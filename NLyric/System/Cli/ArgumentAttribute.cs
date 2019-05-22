namespace System.Cli {
	/// <summary>
	/// 表示一个命令行参数。被应用 <see cref="ArgumentAttribute"/> 的属性必须为 <see cref="string"/> 类型或 <see cref="bool"/> 类型且为实例属性。
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	internal sealed class ArgumentAttribute : Attribute {
		private readonly string _name;
		private bool _isRequired;
		private object _defaultValue;
		private string _type;
		private string _description;

		public string Name => _name;

		public bool IsRequired {
			get => _isRequired;
			set => _isRequired = value;
		}

		public object DefaultValue {
			get => _defaultValue;
			set => _defaultValue = value;
		}

		public string Type {
			get => _type;
			set => _type = value;
		}

		public string Description {
			get => _description;
			set => _description = value;
		}

		/// <summary>
		/// 构造器
		/// </summary>
		/// <param name="name">参数名</param>
		public ArgumentAttribute(string name) {
			_name = name;
		}
	}
}
