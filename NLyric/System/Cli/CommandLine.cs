using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace System.Cli {
	internal static class CommandLine {
		public static T Parse<T>(string[] args) where T : new() {
			if (args == null)
				throw new ArgumentNullException(nameof(args));

			T result;

			if (!TryParse(args, out result))
				throw new FormatException($"Invalid {nameof(args)} or generic parameter {nameof(T)}");
			return result;
		}

		public static bool TryParse<T>(string[] args, out T result) where T : new() {
			if (args == null) {
				result = default;
				return false;
			}

			Dictionary<string, ArgumentInfo> argumentInfos;

			if (!TryGetArgumentInfos(typeof(T), out argumentInfos)) {
				result = default;
				return false;
			}
			result = new T();
			for (int i = 0; i < args.Length; i++) {
				ArgumentInfo argumentInfo;

				if (!argumentInfos.TryGetValue(args[i], out argumentInfo)) {
					// 不是有效参数名
					result = default;
					return false;
				}
				if (argumentInfo.HasSetValue) {
					// 重复设置参数
					result = default;
					return false;
				}
				if (argumentInfo.IsBoolean) {
					// 是 bool 类型，所以不需要其它判断，直接赋值 true
					if (!argumentInfo.TrySetValue(result, true)) {
						result = default;
						return false;
					}
					argumentInfo.HasSetValue = true;
					continue;
				}
				if (i == args.Length - 1) {
					// 需要提供值但是到末尾了，未提供值
					result = default;
					return false;
				}
				else {
					// 提供了值，设置值，并且跳过下一个
					i++;
					if (!argumentInfo.TrySetValue(result, args[i])) {
						result = default;
						return false;
					}
					argumentInfo.HasSetValue = true;
					continue;
				}
			}
			foreach (ArgumentInfo argumentInfo in argumentInfos.Values)
				if (!argumentInfo.HasSetValue)
					// 参数未设置值
					if (argumentInfo.IsRequired) {
						// 是必选参数
						result = default;
						return false;
					}
					else {
						// 是可选参数
						if (!argumentInfo.TrySetValue(result, argumentInfo.DefaultValue)) {
							result = default;
							return false;
						}
					}
			return true;
		}

		public static bool ShowUsage<T>() {
			Type type;
			PropertyInfo[] propertyInfos;
			List<ArgumentInfo> argumentInfos;
			int maxNameLength;
			StringBuilder sb;

			type = typeof(T);
			propertyInfos = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (propertyInfos.Length == 0)
				return false;
			argumentInfos = new List<ArgumentInfo>();
			foreach (PropertyInfo propertyInfo in propertyInfos) {
				ArgumentAttribute attribute;

				if (!VerifyProperty(propertyInfo, out attribute))
					return false;
				if (attribute == null)
					continue;
				argumentInfos.Add(new ArgumentInfo(attribute, propertyInfo));
			}
			maxNameLength = argumentInfos.Max(t => GetArgumentFormat(t).Length);
			sb = new StringBuilder();
			sb.AppendLine("Options:");
			foreach (ArgumentInfo argumentInfo in argumentInfos) {
				sb.Append($"  {GetArgumentFormat(argumentInfo).PadRight(maxNameLength)}  {argumentInfo.Description}");
				if (!argumentInfo.IsRequired)
					sb.Append("  (Optional)");
				sb.AppendLine();
			}
			Console.WriteLine(sb.ToString());
			return true;
		}

		private static bool TryGetArgumentInfos(Type type, out Dictionary<string, ArgumentInfo> argumentInfos) {
			PropertyInfo[] propertyInfos;

			propertyInfos = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (propertyInfos.Length == 0) {
				argumentInfos = null;
				return false;
			}
			argumentInfos = new Dictionary<string, ArgumentInfo>();
			foreach (PropertyInfo propertyInfo in propertyInfos) {
				ArgumentAttribute attribute;

				if (!VerifyProperty(propertyInfo, out attribute)) {
					argumentInfos = null;
					return false;
				}
				if (attribute != null)
					argumentInfos.Add(attribute.Name, new ArgumentInfo(attribute, propertyInfo));
			}
			return true;
		}

		private static bool VerifyProperty(PropertyInfo propertyInfo, out ArgumentAttribute argumentAttribute) {
			object[] attributes;
			Type propertyType;

			argumentAttribute = null;
			attributes = propertyInfo.GetCustomAttributes(typeof(ArgumentAttribute), false);
			if (attributes == null || attributes.Length == 0)
				// 排除未应用 ArgumentAttribute 的属性
				return true;
			if (attributes.Length != 1)
				// ArgumentAttribute 不应该被应用多次
				return false;
			propertyType = propertyInfo.PropertyType;
			if (propertyType != typeof(string) && propertyType != typeof(bool))
				// 检查返回类型
				return false;
			argumentAttribute = (ArgumentAttribute)attributes[0];
			if (string.IsNullOrEmpty(argumentAttribute.Name)) {
				// 检查参数名是否为空
				argumentAttribute = null;
				return false;
			}
			foreach (char item in argumentAttribute.Name)
				if (!((item >= 'a' && item <= 'z') || (item >= 'A' && item <= 'Z') || (item >= '0' && item <= '9') || item == '-' || item == '_')) {
					// 检查参数名是否合法
					argumentAttribute = null;
					return false;
				}
			if (argumentAttribute.IsRequired && argumentAttribute.DefaultValue != null) {
				// 是必选参数但有默认值
				argumentAttribute = null;
				return false;
			}
			if (argumentAttribute.DefaultValue != null && argumentAttribute.DefaultValue.GetType() != propertyType) {
				// 有默认值但默认值的类型与属性的类型不相同
				argumentAttribute = null;
				return false;
			}
			return true;
		}

		private static string GetArgumentFormat(ArgumentInfo argumentInfo) {
			return argumentInfo.IsBoolean
				? argumentInfo.Name
				: argumentInfo.Name + " " + (string.IsNullOrEmpty(argumentInfo.Type) ? "VALUE" : argumentInfo.Type);
		}

		private sealed class ArgumentInfo {
			private readonly ArgumentAttribute _attribute;
			private readonly PropertyInfo _propertyInfo;
			private bool? _cachedIsBoolean;
			private bool _hasSetValue;

			public string Name => _attribute.Name;

			public bool IsRequired => _attribute.IsRequired;

			public object DefaultValue => _attribute.DefaultValue;

			public string Type => _attribute.Type;

			public string Description => _attribute.Description;

			public bool IsBoolean {
				get {
					if (_cachedIsBoolean == null)
						_cachedIsBoolean = _propertyInfo.PropertyType == typeof(bool);
					return _cachedIsBoolean.Value;
				}
			}

			public bool HasSetValue {
				get => _hasSetValue;
				set => _hasSetValue = value;
			}

			public ArgumentInfo(ArgumentAttribute attribute, PropertyInfo propertyInfo) {
				_attribute = attribute;
				_propertyInfo = propertyInfo;
			}

			public bool TrySetValue(object instance, object value) {
				try {
					_propertyInfo.SetValue(instance, value, null);
					return true;
				}
				catch {
					return false;
				}
			}
		}
	}
}
