#region using

using System;
using System.Collections;
using System.Collections.Specialized;
using System.IO;
using System.Web;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using Sitecore.Collections;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Data.Managers;
using Sitecore.Data.Validators;
using Sitecore.Data.Clones;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.IO;
using Sitecore.Layouts;
using Sitecore.Pipelines;
using Sitecore.Pipelines.Save;
using Sitecore.Reflection;
using Sitecore.Resources;
using Sitecore.Shell.Applications.ContentEditor;
using Sitecore.Shell.Applications.ContentEditor.Galleries;
using Sitecore.Shell.Applications.ContentEditor.Pipelines.GetContentEditorFields;
using Sitecore.Shell.Applications.ContentManager.Sidebars;
using Sitecore.Shell.Framework;
using Sitecore.Shell.Framework.Commands;
using Sitecore.Sites;
using Sitecore.Text;
using Sitecore.Web;
using Sitecore.Web.Configuration;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Sheer;
using Sitecore.Web.UI.WebControls.Ribbons;
using Sitecore.Web.UI.XmlControls;
using Sitecore.Workflows;
using Sitecore.Xml;
using ContextMenu = Sitecore.Shell.Framework.ContextMenu;
using Control = System.Web.UI.Control;
using Menu = Sitecore.Web.UI.HtmlControls.Menu;
using MenuItem = Sitecore.Web.UI.HtmlControls.MenuItem;
using Tree = Sitecore.Shell.Applications.ContentManager.Sidebars.Tree;
using Version = Sitecore.Data.Version;

#endregion

namespace Sitecore.Support.Shell.Applications.ContentManager
{
    using Sitecore.StringExtensions;
    using Sitecore.Data.Clones;
    using Sitecore.Shell.Applications.ContentManager;
    using Sitecore.Shell;
    using Sitecore.Shell.Applications.ContentEditor.Gutters;
    using SecurityModel;
    using System.Collections.Generic;
    using System.Reflection;

    /// <summary>
    /// Represents the Content Editor form.
    /// </summary>




    public partial class ContentEditorForm : Sitecore.Shell.Applications.ContentManager.ContentEditorForm
    {


        private static System.Reflection.FieldInfo LastFolderFieldInfo = typeof(Sitecore.Shell.Applications.ContentManager.ContentEditorForm).GetField("_lastFolder", BindingFlags.Instance | BindingFlags.NonPublic);
        private static PropertyInfo HasPendingUpdatePropInfo = typeof(Sitecore.Shell.Applications.ContentManager.ContentEditorForm).GetProperty("HasPendingUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        private static PropertyInfo PendingUpdateItemUriPropInfo = typeof(Sitecore.Shell.Applications.ContentManager.ContentEditorForm).GetProperty("PendingUpdateItemUri", BindingFlags.Instance | BindingFlags.NonPublic);
        private ItemUri LastFolder
        {
            get
            {
                return (ItemUri)ContentEditorForm.LastFolderFieldInfo.GetValue(this);
            }
            set
            {
                ContentEditorForm.LastFolderFieldInfo.SetValue(this, value);
            }
        }
        private bool HasPendingUpdate
        {
            get
            {
                return (bool)ContentEditorForm.HasPendingUpdatePropInfo.GetValue(this);
            }
            set
            {
                ContentEditorForm.HasPendingUpdatePropInfo.SetValue(this, value);
            }
        }
        private ItemUri PendingUpdateItemUri
        {
            get
            {
                return (ItemUri)ContentEditorForm.PendingUpdateItemUriPropInfo.GetValue(this);
            }
            set
            {
                Assert.ArgumentNotNull(value, "value");
                ContentEditorForm.PendingUpdateItemUriPropInfo.SetValue(this, value);
            }
        }
        private bool DisableHistory
        {
            get;
            set;
        }
        static ContentEditorForm()
        {
            // Note: this type is marked as 'beforefieldinit'.
            ContentEditorForm.LastFolderFieldInfo = typeof(Sitecore.Shell.Applications.ContentManager.ContentEditorForm).GetField("_lastFolder", BindingFlags.Instance | BindingFlags.NonPublic);
            ContentEditorForm.HasPendingUpdatePropInfo = typeof(Sitecore.Shell.Applications.ContentManager.ContentEditorForm).GetProperty("HasPendingUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            ContentEditorForm.PendingUpdateItemUriPropInfo = typeof(Sitecore.Shell.Applications.ContentManager.ContentEditorForm).GetProperty("PendingUpdateItemUri", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        void RenderEditor([NotNull] Item item, [NotNull] Item root, [NotNull] Control parent, bool showEditor)
        {
            Assert.ArgumentNotNull(item, "item");
            Assert.ArgumentNotNull(root, "root");
            Assert.ArgumentNotNull(parent, "parent");

            Editor editor;

            bool translating = Registry.GetString("/Current_User/Content Editor/Translate") == "on";

            editor = translating ? new Sitecore.Support.Shell.Applications.ContentManager.Translator() : new Sitecore.Support.Shell.Applications.ContentManager.Editor();

            editor.Render(item, root, FieldInfo, parent, showEditor);

            if (Context.ClientPage.IsEvent)
            {
                ClientCommand command = SheerResponse.SetInnerHtml("ContentEditor", parent);
                command.Attributes["preserveScrollTop"] = "true";
                command.Attributes["preserveScrollElement"] = "EditorPanel";
            }
        }

        void UpdateEditor([NotNull] Item folder, [NotNull] Item root, bool showEditor)
        {
            Assert.ArgumentNotNull(folder, "folder");
            Assert.ArgumentNotNull(root, "root");

            Border parent = new Border();
            ContentEditor.Controls.Clear();
            parent.ID = "Editors";
            Context.ClientPage.AddControl(ContentEditor, parent);

            SheerResponse.SetAttribute("scShowEditor", "value", showEditor ? "1" : "0");

            if (Context.ClientPage.IsEvent)
            {
                SheerResponse.SetAttribute("scLanguage", "value", folder.Language.ToString());
            }

            RenderEditor(folder, root, parent, showEditor);

            UpdateValidatorBar(folder, parent);
        }

        void UpdateValidatorBar([NotNull] Item folder, [NotNull] Border parent)
        {
            Assert.ArgumentNotNull(folder, "folder");
            Assert.ArgumentNotNull(parent, "parent");

            if (!UserOptions.ContentEditor.ShowValidatorBar)
            {
                return;
            }

            Sitecore.Data.Validators.ValidatorCollection validators = BuildValidators(ValidatorsMode.ValidatorBar, folder);

            ValidatorOptions options = new ValidatorOptions(false);

            ValidatorManager.Validate(validators, options);

            string validationResult = ValidatorBarFormatter.RenderValidationResult(validators);

            bool updateValidators = validationResult.IndexOf("Applications/16x16/bullet_square_grey.png", StringComparison.InvariantCulture) >= 0;

            if (Context.ClientPage.IsEvent)
            {
                SheerResponse.Eval("scContent.clearValidatorTimeouts()");
                SheerResponse.SetInnerHtml("ValidatorPanel", validationResult);
                SheerResponse.SetAttribute("scHasValidators", "value", validators.Count > 0 ? "1" : string.Empty);
                SheerResponse.Eval("scContent.updateFieldMarkers()");

                if (updateValidators)
                {
                    SheerResponse.Eval("window.setTimeout(\"scContent.updateValidators()\", " + Settings.Validators.UpdateFrequency + ")");
                }

                SheerResponse.Redraw();
                return;
            }

            Control validatorPanel = parent.FindControl("ValidatorPanel");
            if (validatorPanel == null)
            {
                return;
            }

            validatorPanel.Controls.Add(new LiteralControl(validationResult));

            Control form = Context.ClientPage.FindControl("ContentEditorForm");
            form.Controls.Add(new LiteralControl("<input type=\"hidden\" id=\"scHasValidators\" name=\"scHasValidators\" value=\"" + (validators.Count > 0 ? "1" : string.Empty) + "\"/>"));

            if (updateValidators)
            {
                validatorPanel.Controls.Add(new LiteralControl("<script type=\"text/javascript\" language=\"javascript\">window.setTimeout('scContent.updateValidators()', " + Settings.Validators.UpdateFrequency + ")</script>"));
            }

            validatorPanel.Controls.Add(new LiteralControl("<script type=\"text/javascript\" language=\"javascript\">scContent.updateFieldMarkers()</script>"));
        }

        void Update()
        {
            Item folder;
            Item root;

            ContentEditorDataContext.GetState(out root, out folder);
            if (folder == null)
            {
                return;
            }

            RecentDocuments.AddToRecentDocuments(folder.Uri);
            if (!DisableHistory)
            {
                History.Add(folder.Uri);
            }

            bool isCurrentItemChanged = LastFolder == null || folder.Uri != LastFolder || !Context.ClientPage.IsEvent;
            bool showEditor = isCurrentItemChanged || Context.ClientPage.ClientRequest.Form["scShowEditor"] != "0";

            UpdateEditor(folder, root, showEditor);
            UpdateTree(folder);
            UpdateRibbon(folder, isCurrentItemChanged, showEditor);
            UpdateGutter(folder);

            SheerResponse.SetAttribute("__CurrentItem", "value", folder.Uri.ToString());
            SheerResponse.Eval("scContentEditorUpdated()");

            Context.ClientPage.Modified = false;
        }

        void UpdateTree([NotNull] Item folder)
        {
            Assert.ArgumentNotNull(folder, "folder");

            Item currentRoot;

            if (UserOptions.View.ShowEntireTree && WebUtil.GetQueryString("ro").Length == 0)
            {
                currentRoot = folder.Database.GetRootItem();
            }
            else
            {
                currentRoot = ContentEditorDataContext.GetRoot();
            }

            if (CurrentRoot == currentRoot.ID.ToString())
            {
                return;
            }

            Sidebar.ChangeRoot(currentRoot, folder);

            CurrentRoot = currentRoot.ID.ToString();
        }

        private static void UpdateGutter(Item folder)
        {
            Assert.ArgumentNotNull(folder, "folder");

            List<GutterRenderer> gutterRenderers = GutterManager.GetRenderers();

            string gutter = GutterManager.Render(gutterRenderers, folder);

            SheerResponse.SetInnerHtml("Gutter" + folder.ID.ToShortID(), gutter);
        }

        void UpdateRibbon([NotNull] Item folder, bool isCurrentItemChanged, bool showEditor)
        {
            Assert.ArgumentNotNull(folder, "folder");

            CommandContext commandContext = new CommandContext(folder);
            commandContext.Parameters["ShowEditor"] = showEditor ? "1" : "0";
            commandContext.Parameters["Ribbon.RenderTabs"] = "true";

            Ribbon ribbon = Ribbon;
            if (ribbon == null)
            {
                return;
            }

            ribbon.CommandContext = commandContext;
            ribbon.PreserveActiveStrip = !isCurrentItemChanged;
            ribbon.ActiveStrip = WebUtil.GetQueryString("ras");
            ribbon.CustomizeStrips = true;
            ribbon.ShowContextualTabs = (folder.TemplateID != TemplateIDs.Template);

            string result = HtmlUtil.RenderControl(ribbon);

            if (!Context.ClientPage.IsEvent)
            {
                RibbonPlaceholder.Controls.Add(new LiteralControl(result));
            }
            else
            {
                Sidebar sidebar = Sidebar;
                sidebar.Update(folder.ID, true);
                sidebar.SetActiveItem(folder.ID);

                RibbonPlaceholder.Controls.Clear();

                SheerResponse.Redraw();
                SheerResponse.SetInnerHtml("RibbonPanel", result);
            }

            string activeStrip = ribbon.GetRenderedActiveStrip();

            if (!string.IsNullOrEmpty(activeStrip))
            {
                SheerResponse.SetAttribute("scActiveRibbonStrip", "value", activeStrip);
            }
        }
        protected void OnPreRendered([NotNull] EventArgs e)
        {
            Assert.ArgumentNotNull(e, "e");

            Item item = ContentEditorDataContext.GetFolder();

            if (HasPendingUpdate)
            {
                if (PendingUpdateItemUri != null)
                {
                    ContentEditorDataContext.DisableEvents();
                    ContentEditorDataContext.SetFolder(PendingUpdateItemUri);
                    ContentEditorDataContext.EnableEvents();
                }

                Update();
            }
            else
            {
                Sidebar.Update(item.ID, false);
            }
        }
    }

    public class Translator : Editor
    {
        // Methods
        protected override EditorFormatter GetEditorFormatter() =>
            new TranslatorFormatter();
    }



}

