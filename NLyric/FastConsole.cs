using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace NLyric {
	internal static class FastConsole {
		private const int INTERVAL = 5; // 间隔多少毫秒再次检测是否有新文本
		private const int MAX_INTERVAL = 200; // 间隔多少毫秒后强制输出
		private const int MAX_TEXT_COUNT = 5000; // 超过多少条文本后后强制输出

		private static volatile Thread _singleThread;
		private static bool _isIdle = true;
		private static readonly Queue<(string Text, ConsoleColor Color)> _queue = new Queue<(string Text, ConsoleColor Color)>();
		private static readonly object _ioLock = new object();
		private static readonly object _stLock = new object();
		private static ConsoleColor _lastColor;

		/// <summary>
		/// 设置只允许指定线程写入控制台
		/// </summary>
		public static Thread SingleThread {
			get => _singleThread;
			set {
			relock:
				lock (_stLock) {
					var singleThread = _singleThread;
					if (!(singleThread is null) && Thread.CurrentThread != singleThread) {
						Monitor.Wait(_stLock);
						goto relock;
					}
					// 如果不符合设置设置SingleThread的条件，需要等待
					if (singleThread is null || Thread.CurrentThread == singleThread) {
						_singleThread = value;
						if (value is null)
							Monitor.PulseAll(_stLock);
						// 设置为null则取消阻塞其它线程
					}
				}
			}
		}

		/// <summary>
		/// 单线程锁，化简 <see cref="SingleThread"/>
		/// </summary>
		public static IDisposable SingleThreadLock => new AutoSingleThreadLock();

		public static bool IsIdle => _isIdle;

		public static int QueueCount => _queue.Count;

		static FastConsole() {
			new Thread(IOLoop) {
				Name = $"{nameof(FastConsole)}.{nameof(IOLoop)}",
				IsBackground = true
			}.Start();
		}

		public static void WriteNewLine() {
			WriteLine(string.Empty, ConsoleColor.Gray);
		}

		public static void WriteInfo(string value) {
			WriteLine(value, ConsoleColor.Gray);
		}

		public static void WriteWarning(string value) {
			WriteLine(value, ConsoleColor.Yellow);
		}

		public static void WriteError(string value) {
			WriteLine(value, ConsoleColor.Red);
		}

		public static void WriteLine(string value, ConsoleColor color) {
			Write(value + Environment.NewLine, color);
		}

		public static void Write(string value, ConsoleColor color) {
		relock:
			lock (_stLock) {
				var singleThread = _singleThread;
				if (!(singleThread is null) && Thread.CurrentThread != singleThread) {
					Monitor.Wait(_stLock);
					goto relock;
				}
				lock (((ICollection)_queue).SyncRoot) {
					if (string.IsNullOrEmpty(value))
						color = _lastColor;
					// 优化空行显示
					_queue.Enqueue((value, color));
					_lastColor = color;
				}
				lock (_ioLock)
					Monitor.Pulse(_ioLock);
			}
		}

		public static void WriteException(Exception value) {
			if (value is null)
				throw new ArgumentNullException(nameof(value));

			WriteError(ExceptionToString(value));
		}

		public static void Synchronize() {
			while (!_isIdle || _queue.Count != 0)
				Thread.Sleep(INTERVAL / 3);
		}

		private static string ExceptionToString(Exception exception) {
			if (exception is null)
				throw new ArgumentNullException(nameof(exception));

			var sb = new StringBuilder();
			DumpException(exception, sb);
			return sb.ToString();
		}

		private static void DumpException(Exception exception, StringBuilder sb) {
			sb.AppendLine($"Type: {Environment.NewLine}{exception.GetType().FullName}");
			sb.AppendLine($"Message: {Environment.NewLine}{exception.Message}");
			sb.AppendLine($"Source: {Environment.NewLine}{exception.Source}");
			sb.AppendLine($"StackTrace: {Environment.NewLine}{exception.StackTrace}");
			sb.AppendLine($"TargetSite: {Environment.NewLine}{exception.TargetSite}");
			sb.AppendLine("----------------------------------------");
			if (!(exception.InnerException is null))
				DumpException(exception.InnerException, sb);
		}

		private static void IOLoop() {
			var sb = new StringBuilder();
			while (true) {
				_isIdle = true;
				if (_queue.Count == 0) {
					lock (_ioLock)
						Monitor.Wait(_ioLock);
				}
				_isIdle = false;
				// 等待输出被触发

				int delayCount = 0;
				int oldCount;
				do {
					oldCount = _queue.Count;
					Thread.Sleep(INTERVAL);
					delayCount++;
				} while (_queue.Count > oldCount && delayCount < MAX_INTERVAL / INTERVAL && _queue.Count < MAX_TEXT_COUNT);
				// 也许此时有其它要输出的内容

				var currents = default(Queue<(string Text, ConsoleColor Color)>);
				lock (((ICollection)_queue).SyncRoot) {
					currents = new Queue<(string, ConsoleColor)>(_queue);
					_queue.Clear();
				}
				// 获取全部要输出的内容

				do {
					var (text, color) = currents.Dequeue();
					sb.Length = 0;
					sb.Append(text);
					while (true) {
						if (currents.Count == 0)
							break;
						var (nextText, nextColor) = currents.Peek();
						if (nextColor != color)
							break;
						currents.Dequeue();
						sb.Append(nextText);
					}
					// 合并颜色相同，减少重绘带来的性能损失
					var oldColor = Console.ForegroundColor;
					Console.ForegroundColor = color;
					Console.Write(sb.ToString());
					Console.ForegroundColor = oldColor;
				} while (currents.Count > 0);
			}
		}

		public static ConsoleKeyInfo ReadKey(bool intercept) {
			using (SingleThreadLock)
				return Console.ReadKey(intercept);
		}

		public static string ReadLine() {
			using (SingleThreadLock)
				return Console.ReadLine();
		}

		private sealed class AutoSingleThreadLock : IDisposable {
			public AutoSingleThreadLock() {
				SingleThread = Thread.CurrentThread;
				Synchronize();
			}

			void IDisposable.Dispose() {
				if (SingleThread is null)
					throw new InvalidOperationException();
				SingleThread = null;
			}
		}
	}
}
