using System.Collections.Generic;
using System.Reflection;

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
			if (args == null)
				throw new ArgumentNullException(nameof(args));

			Dictionary<string, CliArgumentInfo> cliArgumentInfoDictionary;

			if (!TryGetCliArgumentInfos(typeof(T), out cliArgumentInfoDictionary)) {
				result = default(T);
				return false;
			}
			result = new T();
			for (int i = 0; i < args.Length; i++) {
				CliArgumentInfo cliArgument;

				if (!cliArgumentInfoDictionary.TryGetValue(args[i].ToUpperInvariant(), out cliArgument)) {
					// 不是有效参数名
					result = default(T);
					return false;
				}
				if (cliArgument.HasSetValue) {
					// 重复设置参数
					result = default(T);
					return false;
				}
				if (cliArgument.IsBoolean) {
					// 是 bool 类型，所以不需要其它判断，直接赋值 true
					if (!cliArgument.TrySetValue(result, true)) {
						result = default(T);
						return false;
					}
					cliArgument.HasSetValue = true;
					continue;
				}
				if (i == args.Length - 1) {
					// 需要提供值但是到末尾了，未提供值
					result = default(T);
					return false;
				}
				else {
					// 提供了值，设置值，并且跳过下一个
					i++;
					if (!cliArgument.TrySetValue(result, args[i])) {
						result = default(T);
						return false;
					}
					cliArgument.HasSetValue = true;
					continue;
				}
			}
			foreach (CliArgumentInfo cliArgumentInfo in cliArgumentInfoDictionary.Values)
				if (!cliArgumentInfo.HasSetValue)
					// 参数未设置值
					if (cliArgumentInfo.IsRequired) {
						// 是必选参数
						result = default(T);
						return false;
					}
					else {
						// 是可选参数
						if (!cliArgumentInfo.TrySetValue(result, cliArgumentInfo.DefaultValue)) {
							result = default(T);
							return false;
						}
					}
			return true;
		}

		private static bool TryGetCliArgumentInfos(Type type, out Dictionary<string, CliArgumentInfo> cliArgumentInfoDictionary) {
			PropertyInfo[] propertyInfos;

			propertyInfos = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (propertyInfos.Length == 0) {
				cliArgumentInfoDictionary = null;
				return false;
			}
			cliArgumentInfoDictionary = new Dictionary<string, CliArgumentInfo>();
			foreach (PropertyInfo propertyInfo in propertyInfos) {
				object[] attributes;
				Type propertyType;
				CliArgumentAttribute cliArgumentAttribute;

				attributes = propertyInfo.GetCustomAttributes(typeof(CliArgumentAttribute), false);
				if (attributes == null || attributes.Length == 0)
					// 排除未应用 CliArgumentAttribute 的属性
					continue;
				propertyType = propertyInfo.PropertyType;
				if (propertyType != typeof(string) && propertyType != typeof(bool)) {
					// 检查返回类型
					cliArgumentInfoDictionary.Clear();
					cliArgumentInfoDictionary = null;
					return false;
				}
				cliArgumentAttribute = (CliArgumentAttribute)attributes[0];
				if (string.IsNullOrEmpty(cliArgumentAttribute.Name)) {
					// 检查参数名是否为空
					cliArgumentInfoDictionary.Clear();
					cliArgumentInfoDictionary = null;
					return false;
				}
				foreach (char item in cliArgumentAttribute.Name)
					if (item == ' ' || item == '\t') {
						// 检查参数名是否存在空格
						cliArgumentInfoDictionary.Clear();
						cliArgumentInfoDictionary = null;
						return false;
					}
				if (cliArgumentAttribute.IsRequired && cliArgumentAttribute.DefaultValue != null) {
					// 是必选参数但有默认值
					cliArgumentInfoDictionary.Clear();
					cliArgumentInfoDictionary = null;
					return false;
				}
				if (cliArgumentAttribute.DefaultValue != null && cliArgumentAttribute.DefaultValue.GetType() != propertyType) {
					// 有默认值但默认值的类型与属性的类型不相同
					cliArgumentInfoDictionary.Clear();
					cliArgumentInfoDictionary = null;
					return false;
				}
				cliArgumentInfoDictionary.Add(cliArgumentAttribute.Name, new CliArgumentInfo(propertyInfo, cliArgumentAttribute));
			}
			return true;
		}

		private sealed class CliArgumentInfo {
			private readonly PropertyInfo _propertyInfo;
			private readonly CliArgumentAttribute _attribute;
			private bool _hasSetValue;
			private bool? _cachedIsBoolean;

			public bool IsBoolean {
				get {
					if (_cachedIsBoolean == null)
						_cachedIsBoolean = _propertyInfo.PropertyType == typeof(bool);
					return _cachedIsBoolean.Value;
				}
			}

			public bool IsRequired => _attribute.IsRequired;

			public object DefaultValue => _attribute.DefaultValue;

			public bool HasSetValue {
				get => _hasSetValue;
				set => _hasSetValue = value;
			}

			public CliArgumentInfo(PropertyInfo property, CliArgumentAttribute info) {
				_propertyInfo = property;
				_attribute = info;
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
