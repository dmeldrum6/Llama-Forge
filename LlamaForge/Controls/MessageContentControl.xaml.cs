using System;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;

namespace LlamaForge.Controls
{
    public partial class MessageContentControl : UserControl
    {
        public static readonly DependencyProperty ContentTextProperty =
            DependencyProperty.Register(nameof(ContentText), typeof(string), typeof(MessageContentControl),
                new PropertyMetadata(string.Empty, OnContentTextChanged));

        public string ContentText
        {
            get => (string)GetValue(ContentTextProperty);
            set => SetValue(ContentTextProperty, value);
        }

        private static void OnContentTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MessageContentControl control)
            {
                control.ParseContent();
            }
        }

        public MessageContentControl()
        {
            InitializeComponent();
        }

        private void ParseContent()
        {
            var items = new ObservableCollection<object>();

            if (string.IsNullOrEmpty(ContentText))
            {
                ContentItemsControl.ItemsSource = items;
                return;
            }

            // Regex to match code blocks with optional language identifier
            // Matches: ```language\ncode\n``` or ```\ncode\n```
            var codeBlockRegex = new Regex(@"```(\w+)?\s*\n(.*?)```", RegexOptions.Singleline);
            var matches = codeBlockRegex.Matches(ContentText);

            var lastIndex = 0;

            foreach (Match match in matches)
            {
                // Add text before the code block
                if (match.Index > lastIndex)
                {
                    var textBefore = ContentText.Substring(lastIndex, match.Index - lastIndex).Trim();
                    if (!string.IsNullOrWhiteSpace(textBefore))
                    {
                        items.Add(new TextBlock { Text = textBefore });
                    }
                }

                // Add the code block
                var language = match.Groups[1].Value;
                var code = match.Groups[2].Value;

                items.Add(new CodeBlock
                {
                    Language = string.IsNullOrEmpty(language) ? "code" : language,
                    Code = code,
                    Document = new TextDocument(new StringTextSource(code)),
                    SyntaxHighlighting = GetSyntaxHighlighting(language)
                });

                lastIndex = match.Index + match.Length;
            }

            // Add remaining text after the last code block
            if (lastIndex < ContentText.Length)
            {
                var textAfter = ContentText.Substring(lastIndex).Trim();
                if (!string.IsNullOrWhiteSpace(textAfter))
                {
                    items.Add(new TextBlock { Text = textAfter });
                }
            }

            // If no code blocks were found, just add the whole content as text
            if (items.Count == 0)
            {
                items.Add(new TextBlock { Text = ContentText });
            }

            ContentItemsControl.ItemsSource = items;
        }

        private IHighlightingDefinition? GetSyntaxHighlighting(string language)
        {
            if (string.IsNullOrEmpty(language))
                return null;

            // Map common language names to AvalonEdit syntax names
            var languageMap = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "cs", "C#" },
                { "csharp", "C#" },
                { "c#", "C#" },
                { "cpp", "C++" },
                { "c++", "C++" },
                { "js", "JavaScript" },
                { "javascript", "JavaScript" },
                { "ts", "TypeScript" },
                { "typescript", "TypeScript" },
                { "py", "Python" },
                { "python", "Python" },
                { "java", "Java" },
                { "xml", "XML" },
                { "html", "HTML" },
                { "css", "CSS" },
                { "sql", "SQL" },
                { "json", "JavaScript" }, // AvalonEdit doesn't have JSON, use JavaScript
                { "php", "PHP" },
                { "vb", "VB" },
                { "vbnet", "VB" },
                { "powershell", "PowerShell" },
                { "bash", "Bash" },
                { "sh", "Bash" }
            };

            var mappedLanguage = languageMap.ContainsKey(language) ? languageMap[language] : language;

            try
            {
                return HighlightingManager.Instance.GetDefinition(mappedLanguage);
            }
            catch
            {
                // If highlighting definition is not found, return null (no syntax highlighting)
                return null;
            }
        }
    }

    // Model classes for different content types
    public class TextBlock
    {
        public string Text { get; set; } = string.Empty;
    }

    public class CodeBlock
    {
        public string Language { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public TextDocument? Document { get; set; }
        public IHighlightingDefinition? SyntaxHighlighting { get; set; }
        public bool HasLanguage => !string.IsNullOrEmpty(Language) && Language != "code";
    }
}
