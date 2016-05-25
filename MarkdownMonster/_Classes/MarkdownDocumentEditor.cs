﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using MarkdownMonster;
using MarkdownMonster.Windows;
using Newtonsoft.Json;
using NHunspell;
using Westwind.Utilities;
using Timer = System.Threading.Timer;

namespace MarkdownMonster
{
    [ComVisible(true)]
    public class MarkdownDocumentEditor 
    {
        public WebBrowser WebBrowser { get; set; }

        public MainWindow Window { get; set;  }

        public MarkdownDocument MarkdownDocument { get; set; }

        public dynamic AceEditor { get; set; }
        public string EditorSyntax { get; set; }


        #region Loading And Initialization
        public MarkdownDocumentEditor(WebBrowser browser)
        {
            WebBrowser = browser;
        }

        public void LoadDocument(MarkdownDocument mdDoc = null)
        {            
            if (mdDoc != null)
                MarkdownDocument = mdDoc;

            if (AceEditor == null)
            {
                WebBrowser.LoadCompleted += OnDocumentCompleted;
                WebBrowser.Navigate(Path.Combine(Environment.CurrentDirectory, "Editor\\editor.htm"));
            }
            SetMarkdown();
            FindSyntaxFromFileType(MarkdownDocument.Filename);
        }


        private void OnDocumentCompleted(object sender, NavigationEventArgs e)
        {
            if (AceEditor == null)
            {
                dynamic doc = WebBrowser.Document;
                var window = doc.parentWindow;

                object t = this as object;
                AceEditor = window.initializeinterop("", this);
                               
               if (EditorSyntax != "markdown")
                    AceEditor.setlanguage(EditorSyntax);                

                WebBrowser.Visibility = Visibility.Visible;
                RestyleEditor();
            }
            SetMarkdown();            
        }

        #endregion

        #region Markdown Access and Manipulation

        public void FindSyntaxFromFileType(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return;

            EditorSyntax = "markdown";

            var ext = Path.GetExtension(MarkdownDocument.Filename).ToLower().Replace(".", "");
            if (ext == "json")
                EditorSyntax = "json";
            else if (ext == "html" || ext == "htm")
                EditorSyntax = "html";

            else if (ext == "xml" || ext == "config")
                EditorSyntax = "xml";
            else if (ext == "js")
                EditorSyntax = "javascript";
            else if (ext == "cs")
                EditorSyntax = "csharp";
            else if (ext == "cshtml")
                EditorSyntax = "razor";
            else if (ext == "css")
                EditorSyntax = "css";
            else if (ext == "prg")
                EditorSyntax = "foxpro";
            
        }

        /// <summary>
        /// Sets the markdown text into the editor control
        /// </summary>
        /// <param name="markdown"></param>
        public void SetMarkdown(string markdown = null)
        {
            if (string.IsNullOrEmpty(markdown) && MarkdownDocument != null)
                markdown = MarkdownDocument.CurrentText;


            if (AceEditor != null)
                AceEditor.setvalue(markdown);
        }

        /// <summary>
        /// Reads the markdown text from the editor control
        /// </summary>
        /// <returns></returns>
        public string GetMarkdown()
        {
            if (AceEditor == null)
                return "";

            MarkdownDocument.CurrentText =  AceEditor.getvalue(false);
            return MarkdownDocument.CurrentText;
        }

        /// <summary>
        /// Saves the active document to file.
        /// 
        /// If there's no active filename a file save dialog
        /// is popped up. 
        /// </summary>
        public void SaveDocument()
        {
            if (MarkdownDocument == null || AceEditor == null)
                return;

            GetMarkdown();
            MarkdownDocument.Save();
            AceEditor.isDirty = false;

            // reload settings if we were editing the app config file.
            var justfile = Path.GetFileName(MarkdownDocument.Filename).ToLower();
            if (justfile == "markdownmonster.json")
            {
                mmApp.Configuration.Read();

                mmApp.SetTheme(mmApp.Configuration.ApplicationTheme,Window);
                mmApp.SetThemeWindowOverride(Window);
                
                foreach (TabItem tab in Window.TabControl.Items)
                {
                    var editor = tab.Tag as MarkdownDocumentEditor;
                    editor.RestyleEditor();
                }
            }
        }

        /// <summary>
        /// Takes action on the selected string in the editor using
        /// predefined commands.
        /// </summary>
        /// <param name="action"></param>
        /// <param name="input"></param>
        /// <param name="style"></param>
        /// <returns></returns>
        public string MarkupMarkdown(string action, string input, string style = null)
        {            
            action = action.ToLower();

            if (string.IsNullOrEmpty(input) && !StringUtils.Inlist(action, new string[] { "image", "href" }))
                return null;

            string html = input;

            if (action == "bold")
                html = "**" + input + "**";
            else if (action == "italic")
                html = "*" + input + "*";
            else if (action == "small")
                html = "<small>" + input + "</small>";
            else if (action == "underline")
                html = "<u>" + input + "</u>";
            else if (action == "h1")
                html = "# " + input;
            else if (action == "h2")
                html = "## " + input;
            else if (action == "h3")
                html = "### " + input;
            else if (action == "h4")
                html = "#### " + input;
            else if (action == "h5")
                html = "##### " + input;

            else if (action == "quote")
            {
                StringBuilder sb = new StringBuilder();
                var lines = StringUtils.GetLines(input);
                foreach (var line in lines)
                {
                    sb.AppendLine("> " + line);
                }
                html = sb.ToString();
            }
            else if (action == "list")
            {
                StringBuilder sb = new StringBuilder();
                var lines = StringUtils.GetLines(input);
                foreach (var line in lines)
                {
                    sb.AppendLine("* " + line);
                }
                html = sb.ToString();
            }
            else if (action == "numberlist")
            {
                StringBuilder sb = new StringBuilder();
                var lines = StringUtils.GetLines(input);
                int ct = 0;
                foreach (var line in lines)
                {
                    ct++;
                    sb.AppendLine($"{ct}. " + line);
                }
                html = sb.ToString();
            }
            else if (action == "href")
            {
                var form = new PasteHref()
                {
                    Owner = Window,
                    LinkText = input,
                    MarkdownFile = MarkdownDocument.Filename
                };

                // check for links in input or on clipboard
                string link = input;
                if (string.IsNullOrEmpty(link))
                    link = Clipboard.GetText();

                if (!(input.StartsWith("http:") || input.StartsWith("https:") || input.StartsWith("mailto:") || input.StartsWith("ftp:")))                
                    link = string.Empty;                
                form.Link = link;

                bool? res = form.ShowDialog();
                if (res != null && res.Value)
                    html = $"[{form.LinkText}]({form.Link})";
            }
            else if (action == "image")
            {
                var form = new PasteImage
                {
                    Owner = Window,
                    ImageText = input,
                    MarkdownFile = MarkdownDocument.Filename
                };


                // check for links in input or on clipboard
                string link = input;
                if (string.IsNullOrEmpty(link))
                    link = Clipboard.GetText();

                if (!(input.StartsWith("http:") || input.StartsWith("https:") || input.StartsWith("mailto:") || input.StartsWith("ftp:")))
                    link = string.Empty;

                if (input.Contains(".png") || input.Contains(".jpg") || input.Contains(".gif"))
                    link = input;

                form.Image = link;

                bool? res = form.ShowDialog();
                if (res != null && res.Value)
                {
                    var image = form.Image;
                    html = $"![{form.ImageText}]({form.Image})";
                }
            }
            else if (action == "code")
            {
                var form = new PasteCode();
                form.Owner = Window;
                form.Code = input;
                form.CodeLanguage = "csharp";

                bool? res = form.ShowDialog();
                if (res != null && res.Value)
                {
                    html = "```" + form.CodeLanguage + "\r\n" +
                           form.Code.Trim() + "\r\n" +
                           "```\r\n";
                }
            }

            return html;
        }


        /// <summary>
        /// Pastes text into the editor at the current 
        /// insertion/selection point. Replaces any 
        /// selected text.
        /// </summary>
        /// <param name="text"></param>
        public void SetSelection(string text)
        {
            if (AceEditor == null)
                return;

            AceEditor.setselection(text);                        
            MarkdownDocument.CurrentText = GetMarkdown();
        }


        /// <summary>
        /// Gets the current selection of the editor
        /// </summary>
        /// <returns></returns>
        public string GetSelection()
        {            
            return AceEditor?.getselection(false);            
        }

        /// <summary>
        /// Focuses the Markdown editor in the Window
        /// </summary>
        public void SetEditorFocus()
        {            
            AceEditor?.setfocus(true);
        }


        /// <summary>
        /// Renders Markdown as HTML
        /// </summary>
        /// <param name="markdown">Markdown text to turn into HTML</param>
        /// <param name="renderLinksExternal">If true creates all links with target='top'</param>
        /// <returns></returns>
        public string RenderMarkdown(string markdown, bool renderLinksExternal = false)
        {
            return this.MarkdownDocument.RenderHtml(markdown, renderLinksExternal);
        }

        /// <summary>
        /// Takes a command  like bold,italic,href etc., reads the
        /// text from editor selection, transforms it and pastes
        /// it back into the document.
        /// </summary>
        /// <param name="action"></param>
        public void ProcessEditorUpdateCommand(string action)
        {
            if (AceEditor == null)
                return;

            string html = AceEditor.getselection(false);
            
            string newhtml = MarkupMarkdown(action, html);

            if (!string.IsNullOrEmpty(newhtml) && newhtml != html)
            {
                SetSelection(newhtml);
                AceEditor.setfocus(true);                
                Window.PreviewMarkdown(this, true);
            }
        }
        #endregion

        #region Callback functions from the Html Editor

        public void SetDirty(bool value)
        {            
            MarkdownDocument.IsDirty = value;                                         
        }

        public void PreviewMarkdownCallback()
        {
            GetMarkdown();                        
            Window.PreviewMarkdownAsync(null,true);
        }


        public void SpecialKey(string key)
        {
            if (key == "ctrl-s")
            {
                Window.Model.SaveCommand.Execute(Window);
            }
            else if (key == "ctrl-n")
            {
                Window.Button_Handler(Window.ButtonNewFile,null);
            }
            else if (key == "ctrl-o")
            {
                Window.Button_Handler(Window.ButtonOpenFile, null);
            }
            else if (key == "ctrl-b")
            {
                Window.Model.ToolbarInsertMarkdownCommand.Execute("bold");
            }
            else if (key == "ctrl-i")
            {
                Window.Model.ToolbarInsertMarkdownCommand.Execute("italic");
            }
            else if (key == "ctrl-l")
            {
                Window.Model.ToolbarInsertMarkdownCommand.Execute("list");
            }
            if (key == "ctrl-k")
            {
                Window.Model.ToolbarInsertMarkdownCommand.Execute("href");
            }
            if (key == "alt-c")
            {
                Window.Model.ToolbarInsertMarkdownCommand.Execute("code");
            }
            if (key == "ctrl-shift-down")
            {
                if (Window.PreviewBrowser.IsVisible)
                {
                    dynamic dom = Window.PreviewBrowser.Document;
                    dom.documentElement.scrollTop += 150;
                }                
            }
            if (key == "ctrl-shift-up")
            {
                if (Window.PreviewBrowser.IsVisible)
                {
                    dynamic dom = Window.PreviewBrowser.Document;
                    dom.documentElement.scrollTop -= 150;
                }
            }

        }

        /// <summary>
        /// Restyles the current editor with configuration settings
        /// </summary>
        public void RestyleEditor()
        {
            try
            {
                AceEditor.settheme(mmApp.Configuration.EditorTheme,
                    mmApp.Configuration.EditorFontSize,
                    mmApp.Configuration.EditorWrapText);

                if (this.EditorSyntax == "markdown" || this.EditorSyntax == "text")
                    AceEditor.enablespellchecking(!mmApp.Configuration.EditorEnableSpellcheck, mmApp.Configuration.EditorDictionary);
                else
                    // always disable for non-markdown text
                    AceEditor.enablespellchecking(true, mmApp.Configuration.EditorDictionary);
            }
            catch { }
        }
        #endregion

        #region SpellChecking interactions
        static Hunspell GetSpellChecker(string language = "EN_US", bool reload = false)
        {
            if (reload || _spellChecker == null)
            {
                string dictFolder = Path.Combine(Environment.CurrentDirectory,"Editor\\");

                string aff = dictFolder + language + ".aff";
                string dic = Path.ChangeExtension(aff,"dic");

                _spellChecker = new Hunspell(aff, dic);

                // Custom Dictionary if any
                string custFile = Path.Combine(mmApp.Configuration.CommonFolder,language + "_custom.txt");
                if (File.Exists(custFile))
                {
                    var lines = File.ReadAllLines(custFile);
                    foreach (var line in lines)
                    {
                        _spellChecker.Add(line);
                    }
                }
            }

            return _spellChecker;
        }
        private static Hunspell _spellChecker = null;

        public bool CheckSpelling(string text,string language = "EN_US",bool reload = false)
        {
            var hun = GetSpellChecker(language, reload);
            return hun.Spell(text);
        }

        public string GetSuggestions(string text, string language = "EN_US", bool reload = false)
        {
            var hun = GetSpellChecker(language, reload); 

            var sugg = hun.Suggest(text).Take(10).ToArray();

            return JsonConvert.SerializeObject(sugg);            
        }

        public void AddWordToDictionary(string word, string lang = "EN_US")
        {
            File.AppendAllText(Path.Combine(mmApp.Configuration.CommonFolder + "\\",  lang + "_custom.txt"),word  + "\n");
            _spellChecker.Add(word);            
        }
        #endregion


    }


}