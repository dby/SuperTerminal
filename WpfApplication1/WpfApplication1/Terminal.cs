using System;
using System.Collections.Generic;
using System.Media;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO;
using System.Windows.Documents;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;

namespace AurelienRibon.Ui.Terminal {
	public class Terminal : RichTextBox {
		protected enum CommandHistoryDirection { BACKWARD, FORWARD }

		public bool IsPromptInsertedAtLaunch { get; set; }
		public bool IsSystemBeepEnabled { get; set; }
		public string Prompt { get; set; }

        private FlowDocument _flowDoc = new FlowDocument();
        public List<string> RegisteredCommands { get; private set; }
		public List<Command> CommandLog { get; private set; }
		public TextPointer LastTextPointer { get; private set; }
		public bool IsInputEnabled { get; private set; }

        public bool isApplyFilterBool = false;
        public string filterString = "";

		public int indexInLog = 0;

		public Terminal() {
			IsUndoEnabled = false;
			AcceptsReturn = false;
			AcceptsTab = false;

			RegisteredCommands = new List<string>();
			CommandLog = new List<Command>();
			IsPromptInsertedAtLaunch = true;
			IsSystemBeepEnabled = true;
            LastTextPointer = CaretPosition.DocumentStart;
			IsInputEnabled = false;

			Loaded += (s, e) => {
				if (IsPromptInsertedAtLaunch)
					InsertNewPrompt();
			};

			TextChanged += (s, e) => {
				ScrollToEnd();
			};
		}

        // --------------------------------------------------------------------
        // PUBLIC INTERFACE
        // --------------------------------------------------------------------
        //
        // RichTextBox 插入 Prompt（"# "） 
        //
        public void InsertNewPrompt() {

            if (Document.Blocks.Count > 200)
                Document.Blocks.Remove(Document.Blocks.ElementAt(0));

            var p = new Paragraph();
            var r = new Run("\n" + Prompt);
            p.Inlines.Add(r);
            p.LineHeight = 1;
            p.FontFamily = new System.Windows.Media.FontFamily("宋体");
            p.Foreground = System.Windows.Media.Brushes.Red;
            Document.Blocks.Add(p);

            this.ScrollToEnd();
            this.CaretPosition = Document.Blocks.LastBlock.ContentEnd;

            LastTextPointer = CaretPosition.DocumentEnd;
			IsInputEnabled = true;
		}

        public void InsertNewPrompt(string suffix)
        {

            if (Document.Blocks.Count > 200)
                Document.Blocks.Remove(Document.Blocks.ElementAt(0));

            var p = new Paragraph();
            var r = new Run("\n" + Prompt + suffix);
            p.Inlines.Add(r);
            p.LineHeight = 1;
            p.FontFamily = new System.Windows.Media.FontFamily("宋体");
            p.Foreground = System.Windows.Media.Brushes.Red;
            Document.Blocks.Add(p);

            this.ScrollToEnd();
            this.CaretPosition = Document.Blocks.LastBlock.ContentEnd;

            LastTextPointer = CaretPosition.DocumentEnd;
            IsInputEnabled = true;
        }

        public void InsertLineBeforePrompt(string text) {
            
            // 若超过200行，则删除第一行
            if (Document.Blocks.Count > 200)
                Document.Blocks.Remove(Document.Blocks.ElementAt(0));

            //string insertedText = text + (text.EndsWith("\n") ? "" : "\n");
            string insertedText = text.Trim();

            var p = new Paragraph();
            var r = new Run(insertedText);
            p.Inlines.Add(r);
            p.LineHeight = 1;
            p.FontFamily = new System.Windows.Media.FontFamily("宋体");

            if (isApplyFilterBool)
            {
                Match regexMatch = Regex.Match(insertedText, filterString);
                if (regexMatch.Success)
                {
                    Console.WriteLine(regexMatch.Groups[0].Value);
                    p.Foreground = System.Windows.Media.Brushes.Red;       //设置字体颜色  
                    p.FontSize = 16;
                }
            }
            else
            {
                p.FontSize = 14;
                p.Foreground = System.Windows.Media.Brushes.Black;
            }

            // 选择插入到倒数第二行
            Document.Blocks.InsertBefore(Document.Blocks.LastBlock, p);
            this.ScrollToEnd();
            this.CaretPosition = Document.Blocks.LastBlock.ContentEnd;
        }

        public bool isCursorInLastLine()
        {
            // 判断 是否 处在最后一行
            if ((this.CaretPosition.CompareTo(Document.Blocks.LastBlock.ContentStart) == 1) && (this.CaretPosition.CompareTo(Document.Blocks.LastBlock.ContentEnd) == -1))
            {
                //Console.WriteLine("处在最后一行..");
                return true;
            }
            return false;
        }

        // --------------------------------------------------------------------
        // EVENT HANDLER
        // --------------------------------------------------------------------

        protected override void OnPreviewKeyDown(KeyEventArgs e) {

			// If Ctrl+C is entered, raise an abortrequested event !
			if (e.Key == Key.C) {
				if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) {
					RaiseAbortRequested();
					e.Handled = true;
					return;
				}
			}

			// Store the length of Text before any input processing.
			//int initialLength = Text.Length;

			// If input is not allowed, warn the user and discard its input.
			if (!IsInputEnabled) {
				if (IsSystemBeepEnabled)
					SystemSounds.Beep.Play();
				e.Handled = true;
				return;
			}

            // Test the caret position.
            //
            // 1. If located before the last prompt index
            //    ==> Warn, set the caret at the end of input text, add text, discard the input
            //        if user tries to erase text, else process it.
            //
            // 2. If located at the last prompt index and user tries to erase text
            //    ==> Warn, discard the input.
            //
            // 3. If located at the last prompt index and user tries to move backward
            //    ==> Warn, discard the input.
            //
            // 4. If located after (>=) the last prompt index and user presses the UP key
            //    ==> Launch command history backward, discard the input.
            //
            // 5. If located after (>=) the last prompt index and user presses the UP key
            //    ==> Launch command history forward, discard the input.
            //

            if (e.Key == Key.Back)
            {
                // 不能删除最后一行 之外的字符串
                if (!isCursorInLastLine())
                {
                    if (IsSystemBeepEnabled)
                        SystemSounds.Beep.Play();
                    e.Handled = true;
                }

                string regExp = "(?<=#.).*";
                TextRange tr = new TextRange(Document.Blocks.LastBlock.ContentStart, Document.Blocks.LastBlock.ContentEnd);
                Match orderMatch = Regex.Match(tr.Text, regExp);
                if (orderMatch.Success)
                {
                    // 最后一行没有内容时，不能再删除，即删除“# ”
                    if (string.Compare(orderMatch.Groups[0].Value, "") == 0)
                    {
                        if (IsSystemBeepEnabled)
                            SystemSounds.Beep.Play();
                        e.Handled = true;
                        return;
                    }
                }
            }
			else if (e.Key == Key.Left && isCursorInLastLine())
            {
                TextRange textRange = new TextRange(Document.Blocks.LastBlock.ContentStart, this.CaretPosition);
                string textRegex = textRange.Text.Substring(0, textRange.Text.Length);
                if (string.Compare(textRegex.TrimStart(), Prompt) == 0)
                {
                    if (IsSystemBeepEnabled)
                        SystemSounds.Beep.Play();
                    e.Handled = true;
                }
			}
            else if (e.Key == Key.Up && isCursorInLastLine())
            {
				HandleCommandHistoryRequest(CommandHistoryDirection.BACKWARD);
				e.Handled = true;
			}
            else if (e.Key == Key.Down && isCursorInLastLine())
            {
				HandleCommandHistoryRequest(CommandHistoryDirection.FORWARD);
				e.Handled = true;
			}

			// If input has not yet been discarded, test the key for special inputs.
			// ENTER   => validates the input
			// TAB     => launches command completion with registered commands
			// CTRL+C  => raises an abort request event
			if (!e.Handled)
            {
				switch (e.Key)
                {
					case Key.Enter:
						HandleEnterKey();
						e.Handled = true;
						break;
					case Key.Tab:
						HandleTabKey();
						e.Handled = true;
						break;
				}
			}

			base.OnPreviewKeyDown(e);
		}

		// --------------------------------------------------------------------
		// VIRTUAL METHODS
		// --------------------------------------------------------------------

		protected virtual void HandleCommandHistoryRequest(CommandHistoryDirection direction) {
            switch (direction)
            {
                case CommandHistoryDirection.BACKWARD:
                    {
                        try
                        {
                            if (indexInLog > 0)
                                indexInLog--;
                            if (CommandLog.Count > 0)
                            {
                                Document.Blocks.Remove(Document.Blocks.LastBlock);
                                InsertNewPrompt(CommandLog[indexInLog].Raw.Trim());

                                this.ScrollToEnd();
                                this.CaretPosition = Document.Blocks.LastBlock.ContentEnd;
                            }

                        }
                        catch (Exception ee)
                        {

                        }
                        break;
                    }

                case CommandHistoryDirection.FORWARD:
                    {
                        try
                        {
                            if (indexInLog < CommandLog.Count - 1)
                                indexInLog++;
                            if (indexInLog == CommandLog.Count - 1)
                            {
                            	InsertNewPrompt();
                            	this.ScrollToEnd();
                                this.CaretPosition = Document.Blocks.LastBlock.ContentEnd;
                            }
                            if (CommandLog.Count > 0)
                            {

                                Document.Blocks.Remove(Document.Blocks.LastBlock);
                                InsertNewPrompt(CommandLog[indexInLog].Raw.Trim());

                                this.ScrollToEnd();
                                this.CaretPosition = Document.Blocks.LastBlock.ContentEnd;
                            }
                        }
                        catch (Exception ee)
                        {
                        }
                        break;
                    }
            }
		}

		protected virtual void HandleEnterKey() {

            //string line = Text.Substring(LastPomptIndex);
            //Text += "\n";

            TextRange textRange = new TextRange(Document.Blocks.LastBlock.ContentStart, Document.Blocks.LastBlock.ContentEnd);

            string regExp = "(?<=#.).*";
            Match orderMatch = Regex.Match(textRange.Text, regExp);
            if (orderMatch.Success)
            {
                IsInputEnabled = true;

                if (string.Compare(orderMatch.Groups[0].Value, "") != 0)
                {
                    Command cmd = TerminalUtils.ParseCommandLine(orderMatch.Groups[0].Value);
                    CommandLog.Add(cmd);
                    indexInLog = CommandLog.Count;
                    RaiseCommandEntered(cmd);
                }               
            }
            else
            {
                MessageBox.Show("Order Match Fail.");
                return;
            }
		}

		protected virtual void HandleTabKey() {
            // Command completion works only if caret is at last character
            // and if the user already typed something.

            // Get command name and associated commands
            string regExp = "(?<=#.).*";
            TextRange textRange = new TextRange(Document.Blocks.LastBlock.ContentStart, Document.Blocks.LastBlock.ContentEnd);
            Match orderMatch = Regex.Match(textRange.Text, regExp);
            if (!orderMatch.Success)
                return;

            string line = orderMatch.Groups[0].Value;
            string[] commands = GetAssociatedCommands(line);

			// If some associated command exist...
			if (commands.Length > 0) {
				// Get the commands common prefix
				string commonPrefix = GetCommonPrefix(commands);
				// If there is no more autocompletion available...
				if (commonPrefix == line) {
					// If there are more than one command to print
					if (commands.Length > 1) {
                        // Print every associated command and insert a new prompt

                        Run r  = null;
                        var p = new Paragraph();
                        p.LineHeight = 1;
                        p.FontFamily = new System.Windows.Media.FontFamily("宋体");
                        foreach (string cmd in commands)
                        {
                            r = new Run(cmd + ",");
                            p.Inlines.Add(r);
                        }
                        p.Inlines.Add(r);
                        Document.Blocks.Add(p);


                        InsertNewPrompt(orderMatch.Groups[0].Value);

                        this.ScrollToEnd();
                        this.CaretPosition = Document.Blocks.LastBlock.ContentEnd;
					}
				} else {
                    // 此时配置成功，且匹配成功的只有一个
                    // Erase the user input
                    Document.Blocks.Remove(Document.Blocks.LastBlock);
                    //Insert the common prefix
                    InsertNewPrompt(commonPrefix);
                    
                    //Set the caret at the end of the text
                    this.CaretPosition = Document.Blocks.LastBlock.ContentEnd;
				}
				return;
			}

			// If no command exists, try path completion
			if (line.Contains("\"") && line.Split('"').Length % 2 == 0) {
				int idx = line.LastIndexOf('"');
				string prefix = line.Substring(0, idx + 1);
				string suffix = line.Substring(idx + 1, line.Length - prefix.Length);
				CompletePath(prefix, suffix);
			} else {
				int idx = Math.Max(line.LastIndexOf(' '), line.LastIndexOf('\t'));
				string prefix = line.Substring(0, idx + 1);
				string suffix = line.Substring(idx + 1, line.Length - prefix.Length);
				CompletePath(prefix, suffix);
			}
		}

		// --------------------------------------------------------------------
		// CLASS SPECIFIC UTILITIES
		// --------------------------------------------------------------------

		protected void CompletePath(string linePrefix, string lineSuffix) {
			if (lineSuffix.Contains("\\") || lineSuffix.Contains("/")) {
				int idx = Math.Max(lineSuffix.LastIndexOf("\\"), lineSuffix.LastIndexOf("/"));
				string dir = lineSuffix.Substring(0, idx + 1);
				string prefix = lineSuffix.Substring(idx + 1, lineSuffix.Length - dir.Length);
				string[] files = GetFileList(dir, lineSuffix[idx] == '\\');

				List<string> commonPrefixFiles = new List<string>();
				foreach (string file in files)
					if (file.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase))
						commonPrefixFiles.Add(file);
				if (commonPrefixFiles.Count > 0) {
					string commonPrefix = GetCommonPrefix(commonPrefixFiles.ToArray());
					if (commonPrefix == prefix) {
//						foreach (string file in commonPrefixFiles)
//							Text += "\n" + file;
						InsertNewPrompt();
//						Text += linePrefix + lineSuffix;
//						CaretIndex = Text.Length;
					} else {
//						Text = Text.Remove(LastPomptIndex);
//						Text += linePrefix + dir + commonPrefix;
//						CaretIndex = Text.Length;
					}
				}
			}
		}

		protected string[] GetAssociatedCommands(string prefix) {
			List<string> ret = new List<string>();
			foreach (var cmd in RegisteredCommands)
				if (cmd.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase))
					ret.Add(cmd);
			return ret.ToArray();
		}

		// --------------------------------------------------------------------
		// GENERAL UTILITIES
		// --------------------------------------------------------------------

		protected string GetShortestString(string[] strs) {
			string ret = strs[0];
			foreach (string str in strs)
				ret = str.Length < ret.Length ? str : ret;
			return ret;
		}

		protected string GetCommonPrefix(string[] strs) {
			string shortestStr = GetShortestString(strs);
			for (int i = 0; i < shortestStr.Length; i++)
				foreach (string str in strs)
					if (char.ToLower(str[i]) != char.ToLower(shortestStr[i]))
						return shortestStr.Substring(0, i);
			return shortestStr;
		}

		protected string[] GetFileList(string dir, bool useAntislash) {
			if (!Directory.Exists(dir))
				return new string[0];
			string[] dirs = Directory.GetDirectories(dir);
			string[] files = Directory.GetFiles(dir);

			for (int i = 0; i < dirs.Length; i++)
				dirs[i] = Path.GetFileName(dirs[i]) + (useAntislash ? "\\" : "/");
			for (int i = 0; i < files.Length; i++)
				files[i] = Path.GetFileName(files[i]);

			List<string> ret = new List<string>();
			ret.AddRange(dirs);
			ret.AddRange(files);
			return ret.ToArray();
		}

		// --------------------------------------------------------------------
		// CUSTOM EVENTS
		// --------------------------------------------------------------------

		public event EventHandler<EventArgs> AbortRequested;
		public event EventHandler<CommandEventArgs> CommandEntered;

		public class CommandEventArgs : EventArgs {
			public Command Command { get; private set; }
			public CommandEventArgs(Command command) {
				Command = command;
			}
		}

		private void RaiseAbortRequested() {
			if (AbortRequested != null)
				AbortRequested(this, new EventArgs());
		}

		public void RaiseCommandEntered(Command command) {
			if (CommandEntered != null)
				CommandEntered(this, new CommandEventArgs(command));
		}
	}
}