namespace System.Cli {
	/// <summary>
	/// 表示一个命令行参数。被应用 <see cref="CliArgumentAttribute"/> 的属性必须为 <see cref="string"/> 类型或 <see cref="bool"/> 类型且为实例属性。
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	internal sealed class CliArgumentAttribute : Attribute {
		private readonly string _name;
		private bool _isRequired;
		private object _defaultValue;

		public string Name => _name;

		public bool IsRequired {
			get => _isRequired;
			set => _isRequired = value;
		}

		public object DefaultValue {
			get => _defaultValue;
			set => _defaultValue = value;
		}

		/// <summary>
		/// 构造器
		/// </summary>
		/// <param name="name">参数名</param>
		public CliArgumentAttribute(string name) => _name = name.ToUpperInvariant();
	}
}
