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

		/// <summary>
		/// 参数名
		/// </summary>
		public string Name => _name;

		/// <summary>
		/// 是否为必选参数
		/// </summary>
		public bool IsRequired {
			get => _isRequired;
			set => _isRequired = value;
		}

		/// <summary>
		/// 默认值，当 <see cref="IsRequired"/> 为 <see langword="true"/> 时，<see cref="DefaultValue"/> 必须为 <see langword="null"/>。
		/// </summary>
		public object DefaultValue {
			get => _defaultValue;
			set => _defaultValue = value;
		}

		/// <summary>
		/// 参数类型，用于 <see cref="CommandLine.ShowUsage{T}"/> 显示类型来简单描述参数。若应用到返回类型为 <see cref="bool"/> 的属性上，<see cref="Type"/> 必须为 <see langword="null"/>。
		/// </summary>
		public string Type {
			get => _type;
			set => _type = value;
		}

		/// <summary>
		/// 参数介绍，用于 <see cref="CommandLine.ShowUsage{T}"/> 具体描述参数。
		/// </summary>
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
