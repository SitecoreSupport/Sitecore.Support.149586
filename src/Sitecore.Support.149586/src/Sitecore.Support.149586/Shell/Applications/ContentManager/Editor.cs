using Sitecore.Analytics.Data;
using Sitecore.Collections;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Clones;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Pipelines;
using Sitecore.Pipelines.GetContentEditorWarnings;
using Sitecore.Pipelines.GetItemPersonalizationVisibility;
using Sitecore.Resources;
using Sitecore.SecurityModel;
using Sitecore.Shell;
using Sitecore.Shell.Applications.ContentEditor.Galleries;
using Sitecore.Shell.Applications.ContentEditor.Pipelines.RenderContentEditor;
using Sitecore.Shell.Framework.Commands;
using Sitecore.Text;
using Sitecore.Web;
using Sitecore.Web.UI;
using Sitecore.Web.UI.HtmlControls;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using static Sitecore.Shell.Applications.ContentManager.Editor;

namespace Sitecore.Support.Shell.Applications.ContentManager
{
    public partial class Editor: Sitecore.Shell.Applications.ContentManager.Editor
    {
        private static FieldInfo FieldInfoFldInfo = typeof(Sitecore.Shell.Applications.ContentManager.Editor).GetField("_fieldInfo", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo ItemFieldInfo = typeof(Sitecore.Shell.Applications.ContentManager.Editor).GetField("_item", BindingFlags.Instance | BindingFlags.NonPublic);

        private static bool IsQuickInfoSectionCollapsed
        {
            get
            {
                UrlString urlString = new UrlString(Registry.GetString("/Current_User/Content Editor/Sections/Collapsed"));
                string text = urlString["QuickInfo"];
                return string.IsNullOrEmpty(text) || text == "1";
            }
        }


        private Hashtable FieldInfoFld
        {
            get
            {
                return (Hashtable)Editor.FieldInfoFldInfo.GetValue(this);
            }
            set
            {
                Editor.FieldInfoFldInfo.SetValue(this, value);
            }
        }

        private Item ItemFld
        {
            get
            {
                return (Item)Editor.ItemFieldInfo.GetValue(this);
            }
            set
            {
                Editor.ItemFieldInfo.SetValue(this, value);
            }
        }
        /// <summary>
        /// The "add search" tab unique identifier
        /// </summary>
        private static readonly string AddSearchTabId = "TAddNewSearch";

        #region Private methods

        /// <summary>
        /// Gets the active tab.
        /// </summary>
        /// <param name="editorTabs">The editor tabs.</param>
        /// <returns>The active tab.</returns>
        static int GetActiveTab([NotNull] IList<EditorTab> editorTabs)
        {
            Assert.ArgumentNotNull(editorTabs, "editorTabs");

            string activeTab = WebUtil.GetFormValue("scActiveEditorTab");

            if (string.IsNullOrEmpty(activeTab))
            {
                return 0;
            }

            for (int n = 0; n < editorTabs.Count; n++)
            {
                EditorTab tab = editorTabs[n];

                if (tab.Id == activeTab)
                {
                    return n;
                }
            }

            return 0;
        }

        /// <summary>
        /// Gets the content tab.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="sections">The sections.</param>
        /// <param name="tabs">The tabs.</param>
        static void GetContentTab([NotNull] Item item, [NotNull] Sections sections, [NotNull] ICollection<EditorTab> tabs)
        {
            Assert.ArgumentNotNull(item, "item");
            Assert.ArgumentNotNull(sections, "sections");
            Assert.ArgumentNotNull(tabs, "tabs");

            var editorTab = GetEditorTab("Content", Translate.Text(Texts.CONTENT), "People/16x16/cube_blue.png", "<content>", false, "100");

            var output = new HtmlTextWriter(new StringWriter());

            RenderContentControls(output, item, sections);

            editorTab.Controls = output.InnerWriter.ToString();

            tabs.Add(editorTab);
        }
        public override void Render(Sitecore.Data.Items.Item item, Sitecore.Data.Items.Item root, System.Collections.Hashtable fieldInfo, System.Web.UI.Control parent, bool showEditor)
        {
            Assert.ArgumentNotNull(fieldInfo, "fieldInfo");
            Assert.ArgumentNotNull(parent, "parent");

            ItemFld = item;
            FieldInfoFld = fieldInfo;

            fieldInfo.Clear();
            Sections sections = GetSections();

            bool readOnly = ItemFld != null ? GetReadOnly(ItemFld) : true;

            var renderContentEditorArgs = new RenderContentEditorArgs
            {
                EditorFormatter = GetEditorFormatter(),
                Item = ItemFld,
                Parent = parent,
                Sections = sections,
                ReadOnly = readOnly,
                Language = Language,
                IsAdministrator = IsAdministrator,
            };

            Render(renderContentEditorArgs, parent);
        }

        void RenderFullscreenWarnings(RenderContentEditorArgs args, List<GetContentEditorWarningsArgs.ContentEditorWarning> warnings)
        {
            Assert.ArgumentNotNull(args, "args");
            Assert.ArgumentNotNull(warnings, "warnings");

            foreach (GetContentEditorWarningsArgs.ContentEditorWarning warning in warnings)
            {
                if (!warning.IsFullscreen)
                {
                    continue;
                }

                Border border = new Border();

                border.Style["width"] = "100%";
                border.Style["height"] = "100%";
                border.Style["background"] = "white";

                Context.ClientPage.AddControl(args.Parent, border);

                RenderWarning(args, border, warning);

                break;
            }
        }

        static void RenderWarning(RenderContentEditorArgs args, System.Web.UI.Control parent, GetContentEditorWarningsArgs.ContentEditorWarning warning)
        {
            Assert.ArgumentNotNull(args, "args");
            Assert.ArgumentNotNull(parent, "parent");
            Assert.ArgumentNotNull(warning, "warning");

            HtmlTextWriter output = new HtmlTextWriter(new StringWriter());

            output.Write("<div class=\"scEditorWarningPanel\">");

            output.Write("<table border=\"0\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" class=\"scEditorWarningPanelTable\">");
            output.Write("<tr>");
            output.Write("<td valign=\"top\">");

            ImageBuilder imageBuilder = new ImageBuilder();
            imageBuilder.Src = warning.Icon;
            imageBuilder.Class = "scEditorSectionCaptionIcon";
            output.Write(imageBuilder.ToString());

            output.Write("</td>");
            output.Write("<td width=\"100%\">");

            output.Write("<div class=\"scEditorWarningTitle\">");
            output.Write(warning.Title);
            output.Write("</div>");

            if (!string.IsNullOrEmpty(warning.Text))
            {
                output.Write("<div class=\"scEditorWarningHelp\">");
                output.Write(warning.Text);
                output.Write("</div>");
            }

            if (warning.Options.Count > 0)
            {
                output.Write("<div class=\"scEditorWarningOptions\">");
                output.Write("<ul class=\"scEditorWarningOptionsList\">");

                foreach (Pair<string, string> option in warning.Options)
                {
                    output.Write("<li class=\"scEditorWarningOptionBullet\">");
                    var part1 = Translate.Text(option.Part1);
                    string click = option.Part2;
                    if (string.IsNullOrEmpty(click))
                    {
                        output.Write(part1);
                    }
                    else
                    {
                        if (!click.StartsWith("javascript:", StringComparison.InvariantCulture))
                        {
                            click = Context.ClientPage.GetClientEvent(click);
                        }

                        output.Write("<a href=\"#\" class=\"scEditorWarningOption\" onclick=\"");
                        output.Write(click);
                        output.Write("\">");
                        output.Write(part1);
                        output.Write("</a>");
                    }

                    output.Write("</li>");
                }

                output.Write("</ul>");
                output.Write("</div>");
            }

            output.Write("</td>");
            output.Write("</tr>");
            output.Write("</table>");

            output.Write("</div>");

            args.EditorFormatter.AddLiteralControl(parent, output.InnerWriter.ToString());
        }
        void RenderHeaderPanel([NotNull] RenderContentEditorArgs args)
        {
            Assert.ArgumentNotNull(args, "args");

            Item item = args.Item;
            if (item == null)
            {
                return;
            }

            HtmlTextWriter output = new HtmlTextWriter(new StringWriter());

            output.Write("<div class=\"scEditorHeader\">");

            // icon
            ImageBuilder image = new ImageBuilder();
            UrlString url = new UrlString(Images.GetThemedImageSource(item.Appearance.Icon, ImageDimension.id32x32));
            url["rev"] = item[FieldIDs.Revision];
            url["la"] = item.Language.ToString();
            image.Src = url.ToString();
            image.Class = "scEditorHeaderIcon";

            if (item.Appearance.ReadOnly || !item.Access.CanWrite())
            {
                output.Write("<span class=\"scEditorHeaderIcon\">");
                output.Write(image.ToString());
                output.Write("</span>");
            }
            else
            {
                output.Write("<a href=\"#\" class=\"scEditorHeaderIcon\" onclick=\"javascript:return scForm.invoke('item:selecticon')\">");
                output.Write(image.ToString());
                output.Write("</a>");
            }

            // header
            string name = item.DisplayName;

            output.Write("<div class=\"scEditorHeaderTitlePanel\">");

            if (IsAdministrator && name != item.Name)
            {
                name += "<span class=\"scEditorHeaderTitleName\"> - [" + item.Name + "]</span>";
            }

            if (item.Appearance.ReadOnly || !item.Access.CanWrite() || (!Context.IsAdministrator && item.Locking.IsLocked() && !item.Locking.HasLock()))
            {
                output.Write("<div class=\"scEditorHeaderTitle\">" + name + "</div>");
            }
            else
            {
                output.Write("<a href=\"#\" class=\"scEditorHeaderTitle\" onclick=\"javascript:return scForm.invoke('item:rename')\">" + name + "</a>");
            }

            if (item.Help.ToolTip.Length > 0)
            {
                output.Write("<div class=\"scEditorHeaderTitleHelp\">{0}</div>", Settings.ContentEditor.RenderItemHelpAsHtml ? WebUtil.RemoveAllScripts(item.Help.ToolTip) : HttpUtility.HtmlEncode(item.Help.ToolTip));
            }

            output.Write("</div>");

            this.RenderProfileCards(item, output);

            output.Write("</div>");

            args.EditorFormatter.AddLiteralControl(args.Parent, output.InnerWriter.ToString());
        }
        private bool RenderPersonalizationPanel([CanBeNull] Item item)
        {
            CorePipeline pipeline = CorePipelineFactory.GetPipeline("getItemPersonalizationVisibility", string.Empty);
            if (pipeline == null)
            {
                return true;
            }

            var args = new GetItemPersonalizationVisibilityArgs(true, item);
            CorePipeline.Run("getItemPersonalizationVisibility", args);

            return args.Visible;
        }
        private void RenderProfileCards([CanBeNull]Item item, [NotNull]HtmlTextWriter output)
        {
            if (item == null)
            {
                return;
            }

            if (!this.RenderPersonalizationPanel(item))
            {
                return;
            }

            Assert.ArgumentNotNull(output, "output");
            string tooltipText = Translate.Text(Sitecore.Texts.EdittheProfileCardsassociatedwiththisitem);
            var image = new ImageBuilder();
            var url = new UrlString(Images.GetThemedImageSource("BusinessV2/32x32/chart_radar.png", ImageDimension.id32x32));
            image.Src = url.ToString();
            image.Class = "scEditorHeaderCustomizeProfilesIcon";
            image.Alt = tooltipText;

            if (!(item.Appearance.ReadOnly || !item.Access.CanWrite()))
            {
                output.Write("<a href=\"#\" class=\"scEditorHeaderCustomizeProfilesIcon\" onclick=\"javascript:return scForm.invoke('item:personalize')\" title=\"" + tooltipText + "\">");
                output.Write(image.ToString());
                output.Write("</a>");
            }

            StringBuilder builder = new StringBuilder();
            HtmlTextWriter tmpOutput = new HtmlTextWriter(new StringWriter(builder));
            bool hasCardsConfigured = false;
            this.RenderProfileCardIcons(item, tmpOutput, out hasCardsConfigured);
            tmpOutput.Flush();
            if (hasCardsConfigured)
            {
                if (!UIUtil.IsIE())
                {
                    output.Write("<span class=\"scEditorHeaderProfileCards\">");
                }

                output.Write(builder.ToString());

                if (!UIUtil.IsIE())
                {
                    output.Write("</span>");
                }
            }
        }

        private void RenderEditorHeaderSeparator(HtmlTextWriter output, string customClassName)
        {
            string className = string.Empty;
            if (!string.IsNullOrEmpty(customClassName))
            {
                className = " " + customClassName;
            }

            output.Write(string.Format("<div class=\"scEditorHeaderSeperator\"><span class=\"scEditorHeaderSeperatorLine{0}\"></span></div>", className));
        }
        private void RenderProfileCardIcon([NotNull]Item contextItem, [NotNull]Item profileItem, [CanBeNull]Item presetItem, [NotNull]HtmlTextWriter output)
        {
            Assert.ArgumentNotNull(contextItem, "contextItem");
            Assert.ArgumentNotNull(profileItem, "profileItem");
            Assert.ArgumentNotNull(output, "output");

            string imageSrc = presetItem != null ? Sitecore.Support.Analytics.Data.ProfileUtil.UI.GetPresetThumbnail(presetItem) : Sitecore.Support.Analytics.Data.ProfileUtil.UI.GetProfileThumbnail(profileItem);
            var image = new ImageBuilder();
            var url = new UrlString(imageSrc);
            image.Src = url.ToString();
            image.Class = "scEditorHeaderProfileCardIcon";
            string uniqueId = Sitecore.Web.UI.HtmlControls.Control.GetUniqueID("profileIcon");
            string tooltipData = presetItem == null ? profileItem.ID.ToShortID().ToString() + "|" + profileItem.Language.ToString() : presetItem.ID.ToShortID().ToString() + "|" + presetItem.Language.ToString();
            if (contextItem.Appearance.ReadOnly || !contextItem.Access.CanWrite())
            {
                output.Write(string.Format("<a id=\"{4}\" href=\"#\" class=\"scEditorHeaderProfileCardIcon\" style=\"background-image:url('{0}'); background-repeat:no-repeat; background-position:center;\" onmouseover=\"showToolTipWithTimeout('{4}', '{5}', null, 500);\" onmouseout=\"cancelRadTooltip();\">", url.ToString(), string.Empty, string.Empty, string.Empty, uniqueId, tooltipData));
                output.Write("</a>");
            }
            else
            {
                var command = Sitecore.Support.Analytics.Data.ProfileUtil.UI.GetPersonalizeProfileCommand(contextItem, profileItem);
                output.Write(string.Format("<a id=\"{2}\" href=\"#\" class=\"scEditorHeaderProfileCardIcon\" onclick=\"javascript:return scForm.invoke('{1}')\" style=\"background-image:url('{0}'); background-repeat:no-repeat; background-position:center;\" onmouseover=\"showToolTipWithTimeout('{2}', '{3}', null, 500);\" onmouseout=\"cancelRadTooltip();\">", url, command, uniqueId, tooltipData));
                output.Write("</a>");
            }
        }
        private void RenderProfileCardIcons([CanBeNull]Item item, [NotNull]HtmlTextWriter output, out bool hasCardsConfigured)
        {
            hasCardsConfigured = false;
            Assert.ArgumentNotNull(output, "output");
            if (item == null)
            {
                return;
            }

            TrackingField trackingField;
            var profiles = Sitecore.Support.Analytics.Data.ProfileUtil.GetProfiles(item, out trackingField);
            if (trackingField == null)
            {
                return;
            }

            int cardsCount = 0;
            foreach (var profile in profiles)
            {
                if (profile == null)
                {
                    continue;
                }

                var profileItem = profile.GetProfileItem();
                if (profileItem == null)
                {
                    continue;
                }

                if ((profile.Presets == null) || (profile.Presets.Count == 0))
                {
                    if (ProfileUtil.HasPresetData(profileItem, trackingField))
                    {
                        this.RenderEditorHeaderSeparator(output, cardsCount == 0 ? "scEditorHeaderSeperatorFirstLine" : string.Empty);
                        this.RenderProfileCardIcon(item, profileItem, null, output);
                        cardsCount++;
                    }

                    continue;
                }

                int renderedPresetCount = 0;
                foreach (var preset in profile.Presets)
                {
                    var presetItem = profile.GetPresetItem(preset.Key);
                    if (presetItem == null)
                    {
                        continue;
                    }

                    if (renderedPresetCount == 0)
                    {
                        this.RenderEditorHeaderSeparator(output, cardsCount == 0 ? "scEditorHeaderSeperatorFirstLine" : string.Empty);
                    }

                    this.RenderProfileCardIcon(item, profileItem, presetItem, output);
                    cardsCount++;
                    renderedPresetCount++;
                }
            }

            hasCardsConfigured = cardsCount > 0;
        }

        void RenderAllWarnings(RenderContentEditorArgs args, List<GetContentEditorWarningsArgs.ContentEditorWarning> warnings)
        {
            Assert.ArgumentNotNull(args, "args");
            Assert.ArgumentNotNull(warnings, "warnings");

            foreach (GetContentEditorWarningsArgs.ContentEditorWarning warning in warnings)
            {
                if (!warning.IsExclusive)
                {
                    continue;
                }

                RenderWarning(args, args.Parent, warning);
                if (warning.Key == HasNoVersions.Key)
                {
                    var firstVersionAddedWarning = warnings.FirstOrDefault(w => w.Key == FirstVersionAddedNotification.Key);
                    if (firstVersionAddedWarning != null)
                    {
                        RenderWarning(args, args.Parent, firstVersionAddedWarning);
                    }
                }

                return;
            }

            foreach (GetContentEditorWarningsArgs.ContentEditorWarning warning in warnings)
            {
                RenderWarning(args, args.Parent, warning);
            }
        }
        static void RenderQuickInfo([NotNull] RenderContentEditorArgs args)
        {
            Assert.ArgumentNotNull(args, "args");

            Item item = args.Item;
            if (item == null)
            {
                return;
            }

            bool isCollapsed = IsQuickInfoSectionCollapsed;
            bool renderFields = UserOptions.ContentEditor.RenderCollapsedSections;

            args.EditorFormatter.RenderSectionBegin(args.Parent, "QuickInfo", "QuickInfo", Texts.QUICK_INFO, "Applications/16x16/information.png", isCollapsed, renderFields);

            if (renderFields || !isCollapsed)
            {
                HtmlTextWriter output = new HtmlTextWriter(new StringWriter());

                output.Write("<table class='scEditorQuickInfo'>");
                output.Write("<col style=\"white-space:nowrap\" valign=\"top\" />");
                output.Write("<col style=\"white-space:nowrap\" valign=\"top\" />");

                RenderQuickInfoID(output, item);
                RenderQuickInfoItemKey(output, item);
                RenderQuickInfoPath(output, item);
                RenderQuickInfoTemplate(output, item);
                RenderQuickInfoCreatedFrom(output, item);
                RenderQuickInfoOwner(output, item);

                output.Write("</table>");

                args.EditorFormatter.AddLiteralControl(args.Parent, output.InnerWriter.ToString());
            }

            args.EditorFormatter.RenderSectionEnd(args.Parent, renderFields, isCollapsed);
        }

        static void RenderQuickInfoOwner([NotNull] HtmlTextWriter output, [NotNull] Item item)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(item, "item");

            output.Write("<tr><td>");
            output.Write(Translate.Text(Texts.ITEM_OWNER));
            output.Write("</td><td>");

            string owner = item[FieldIDs.Owner];

            if (string.IsNullOrEmpty(owner))
            {
                owner = Translate.Text(Texts.UNKNOWN);
            }

            output.Write("<input class=\"scEditorHeaderQuickInfoInputID\" readonly=\"readonly\" onclick=\"javascript:this.select();return false\" value=\"" + HttpUtility.HtmlEncode(owner) + "\"/>");

            output.Write("</td></tr>");
        }

        static void RenderQuickInfoCreatedFrom([NotNull] HtmlTextWriter output, [NotNull] Item item)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(item, "item");

            output.Write("<tr><td>");
            output.Write(Translate.Text(Texts.CREATED_FROM));
            output.Write("</td><td>");

            if (ItemUtil.IsNull(item.BranchId) && !item.IsClone)
            {
                output.Write(Translate.Text(Texts.UNKNOWN));
            }
            else
            {
                Item master;

                using (new SecurityDisabler())
                {
                    if (item.Source != null)
                    {
                        master = item.Source;
                    }
                    else
                    {
                        master = item.Database.GetItem(item.BranchId);
                    }
                }

                if (master != null && master.Access.CanRead())
                {
                    output.Write("<a href=\"#\" onclick=\"javascript:scForm.postRequest('','','','item:load(id=" + master.ID + ")');return false\">");
                }

                if (master != null)
                {
                    output.Write(master.DisplayName);
                    output.Write(", ");
                    output.Write(master.Language);
                    output.Write(", ");
                    output.Write(master.Version);
                }
                else
                {
                    output.Write(Translate.Text(Texts.BRANCH_NO_LONGER_EXISTS));
                }

                if (master != null && master.Access.CanRead())
                {
                    output.Write("</a>");
                }

                output.Write(" - ");

                output.Write("<input class=\"scEditorHeaderQuickInfoInputID\" readonly=\"readonly\" onclick=\"javascript:this.select();return false\" value=\"" + (master != null ? master.ID : Sitecore.Data.ID.Null) + "\"/>");
            }
            output.Write("</td></tr>");
        }

        static void RenderQuickInfoPath([NotNull] HtmlTextWriter output, [NotNull] Item item)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(item, "item");

            output.Write("<tr><td>");
            output.Write(Translate.Text(Texts.ITEM_PATH));
            output.Write("</td><td>");
            output.Write("<input class=\"scEditorHeaderQuickInfoInput\" readonly=\"readonly\" onclick=\"javascript:this.select();return false\" value=\"" + item.Paths.Path + "\"/>");
            output.Write("</td></tr>");
        }

        /// <summary>
        /// Renders the quick info template.
        /// </summary>
        /// <param name="output">The output.</param>
        /// <param name="item">The item.</param>
        static void RenderQuickInfoTemplate([NotNull] HtmlTextWriter output, [NotNull] Item item)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(item, "item");

            output.Write("<tr><td>");
            output.Write(Translate.Text(Texts.TEMPLATE));
            output.Write("</td><td>");

            Item template;

            using (new SecurityDisabler())
            {
                template = item.Database.GetItem(item.TemplateID);
            }

            bool canEditTemplate = template != null && CommandManager.QueryState("shell:edittemplate", item) == CommandState.Enabled;

            if (canEditTemplate)
            {
                output.Write("<a href=\"#\" onclick=\"javascript:scForm.postRequest('','','','shell:edittemplate');return false\">");
            }

            if (template != null)
            {
                output.Write(template.Paths.Path);
            }
            else
            {
                output.Write(Translate.Text(Texts.TEMPLATE_NO_LONGER_EXISTS));
            }

            if (canEditTemplate)
            {
                output.Write("</a>");
            }

            output.Write(" - ");

            output.Write("<input class=\"scEditorHeaderQuickInfoInputID\" readonly=\"readonly\" onclick=\"javascript:this.select();return false\" value=\"" + item.TemplateID + "\"/>");

            output.Write("</td></tr>");
        }

        static void RenderQuickInfoID([NotNull] HtmlTextWriter output, [NotNull] Item item)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(item, "item");

            output.Write("<tr><td>");
            output.Write(Translate.Text(Texts.ITEM_ID));
            output.Write("</td><td>");
            output.Write("<input class=\"scEditorHeaderQuickInfoInput\" readonly=\"readonly\" onclick=\"javascript:this.select();return false\" value=\"" + item.ID + "\"/>");
            output.Write("</td></tr>");
        }

        /// <summary>
        /// Renders the quick info item key.
        /// </summary>
        /// <param name="output">The output.</param>
        /// <param name="item">The item.</param>
        static void RenderQuickInfoItemKey([NotNull] HtmlTextWriter output, [NotNull] Item item)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(item, "item");

            output.Write("<tr><td>");
            output.Write(Translate.Text(Texts.ITEM_NAME));
            output.Write("</td><td>");
            output.Write(MakeSelectableText(item.Name));

            if (item.DisplayName != item.Name)
            {
                output.Write(" - ");
                output.Write(Translate.Text(Texts.DISPLAY_NAME));
                output.Write(" ");
                output.Write(MakeSelectableText(item.DisplayName));
            }

            output.Write("</td></tr>");
        }

        private static string MakeSelectableText(string value)
        {
            return string.Format("<span onclick=\"javascript: if(window.getSelection) {{var r=document.createRange();r.selectNodeContents(this);var s=window.getSelection(); s.removeAllRanges(); s.addRange(r);}}\">{0}</span>", value);
        }

        protected void Render(RenderContentEditorArgs args, System.Web.UI.Control parent)
        {
            Assert.ArgumentNotNull(args, "args");
            Assert.ArgumentNotNull(parent, "parent");

            args.ShowSections = ShowSections;
            args.ShowInputBoxes = ShowInputBoxes;
            args.EditorFormatter.Arguments = args;
            args.RenderTabsAndBars = RenderTabsAndBars;

            GetContentEditorWarningsArgs warnings = GetWarnings(args.Sections.Count > 0);

            if (warnings.HasFullscreenWarnings())
            {
                RenderFullscreenWarnings(args, warnings.Warnings);
                return;
            }

            args.Parent = RenderEditorTabs(args);

            if (ShouldShowHeader())
            {
                RenderHeaderPanel(args);
            }

            RenderAllWarnings(args, warnings.Warnings);

            if (UserOptions.ContentEditor.ShowQuickInfo && ShouldShowHeader())
            {
                RenderQuickInfo(args);
            }

            if (warnings.HideFields())
            {
                return;
            }

            using (new LongRunningOperationWatcher(Settings.Profiling.RenderFieldThreshold, "renderContentEditor pipeline[id={0}]", ItemFld != null ? ItemFld.ID.ToString() : string.Empty))
            {
                CorePipeline.Run("renderContentEditor", args);
            }
        }

        static void RenderContentControls([NotNull] HtmlTextWriter output, [NotNull] Item item, [NotNull] Sections sections)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(item, "item");
            Assert.ArgumentNotNull(sections, "sections");

            output.Write("<div class=\"scEditorTabControls\">");

            new ImageBuilder { Src = "Images/Ribbon/tab4.png", Class = "scEditorTabControlsTab4" }.ToString(output);

            output.Write("<span class=\"scEditorTabControlsTab5\">");

            // RenderHeaderPublishing(output, item);
            // RenderHeaderWorkflow(output, item);
            RenderHeaderNavigator(output, sections);
            RenderHeaderLanguage(output, item);
            RenderHeaderVersion(output, item);

            output.Write("</span>");

            new ImageBuilder { Src = "Images/Ribbon/tab6.png", Class = "scEditorTabControlsTab6" }.ToString(output);

            output.Write("</div>");
        }
        static void RenderHeaderNavigator([NotNull] HtmlTextWriter output, [NotNull] Sections sections)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(sections, "sections");

            new Tag("a")
            {
                ID = "ContentEditorNavigator",
                Class = "scEditorHeaderNavigator scEditorHeaderButton",
                Href = "#",
                Title = Translate.Text(Texts.NAVIGATES_TO_SECTIONS_AND_FIELDS),
                Click = "javascript:return scForm.postEvent(this,event,'NavigatorMenu_DropDown()')"
            }.Start(output);

            var image = new ImageBuilder { Src = "Applications/16x16/bookmark.png", Class = "scEditorHeaderNavigatorIcon", Alt = Translate.Text(Texts.NAVIGATES_TO_SECTIONS_AND_FIELDS) };
            output.Write(image.ToString());

            image = new ImageBuilder { Src = "Images/ribbondropdown.gif", Class = "scEditorHeaderNavigatorGlyph" };
            output.Write(image.ToString());

            output.Write("</a>");
        }
        static void RenderHeaderLanguage([NotNull] HtmlTextWriter output, [NotNull] Item item)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(item, "item");

            const string FrameID = "Header_Language_Gallery";

            UrlString url = new UrlString();
            url.Append("id", item.ID.ToString());
            url.Append("la", item.Language.ToString());
            url.Append("vs", item.Version.ToString());
            url.Append("db", item.Database.Name);
            url.Append("align", "right");

            string width = "500";
            string height = "250";

            GalleryManager.GetGallerySize(FrameID, ref width, ref height);

            string click = "javascript:return scContent.showGallery(this,event,'" + FrameID + "','Gallery.Languages','" + url + "','" + width + "','" + height + "')";
            using (new ThreadCultureSwitcher(Context.Language.CultureInfo))
            {
                new Tag("a") { Href = "#", Class = "scEditorHeaderVersionsLanguage scEditorHeaderButton", Title = item.Language.CultureInfo.DisplayName, Click = click }.Start(output);

                var image = new ImageBuilder();

                string icon = LanguageService.GetIcon(item.Language, item.Database);

                image.Src = Images.GetThemedImageSource(icon, ImageDimension.id16x16);
                image.Class = "scEditorHeaderVersionsLanguageIcon";
                image.Alt = item.Language.CultureInfo.DisplayName;
                output.Write(image.ToString());

                image = new ImageBuilder { Src = "Images/ribbondropdown.gif", Class = "scEditorHeaderVersionsLanguageGlyph" };
                output.Write(image.ToString());
            }

            output.Write("</a>");
        }
        static void RenderHeaderVersion([NotNull] HtmlTextWriter output, [NotNull] Item item)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(item, "item");

            int count = item.Versions.Count;

            if (count <= 0)
            {
                return;
            }

            const string FrameID = "Header_Version_Gallery";

            UrlString url = new UrlString();
            url.Append("id", item.ID.ToString());
            url.Append("la", item.Language.ToString());
            url.Append("vs", item.Version.ToString());
            url.Append("db", item.Database.Name);
            url.Append("align", "right");

            string width = "500";
            string height = "250";

            GalleryManager.GetGallerySize(FrameID, ref width, ref height);

            string click = "javascript:return scContent.showGallery(this, event, '" + FrameID + "','Gallery.Versions','" + url + "','" + width + "','" + height + "')";

            string title = Translate.Text(Texts.VERSION_0_OF_1, item.Version, count);

            new Tag("a") { Href = "#", Class = "scEditorHeaderVersionsVersion scEditorHeaderButton", Title = title, Click = click }.Start(output);
            output.Write(item.Version);

            output.Write(new ImageBuilder { Src = "Images/ribbondropdown.gif", Class = "scEditorHeaderVersionsVersionGlyph" });

            output.Write("</a>");
        }
        /// <summary>
        /// Gets the content tab.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="sections">The sections.</param>
        /// <param name="tabs">The tabs.</param>
        static void GetNewSearchTab([NotNull] Item item, [NotNull] Sections sections, [NotNull] ICollection<EditorTab> tabs)
        {
            Assert.ArgumentNotNull(item, "item");
            Assert.ArgumentNotNull(tabs, "tabs");

            var list = new ListString();
            list.Add("{4C76D96D-3343-404A-834B-0DC4DABB5EE3}");

            foreach (string id in list)
            {
                var editor = Client.CoreDatabase.GetItem(id);

                if (editor == null)
                {
                    continue;
                }

                string urlString = string.Empty;
                string editorUrl = editor["Url"];
                if (!string.IsNullOrEmpty(editorUrl))
                {
                    var url = new UrlString(editor["Url"]);
                    url["id"] = item.ID.ToString();
                    url["la"] = item.Language.ToString();
                    url["language"] = item.Language.ToString();
                    url["vs"] = item.Version.ToString();
                    url["version"] = item.Version.ToString();
                    urlString = url.ToString();
                }


                var editorTab = GetEditorTab(AddSearchTabId, editor["Header"], editor["Icon"], urlString, editor["Refresh On Show"] == "1", editor["{00379E66-3C61-4296-A56E-67F531A2D8FB}"]);

                tabs.Add(editorTab);
            }
        }

        /// <summary>
        /// Gets the custom editor tab.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="tabs">The tabs.</param>
        static void GetCustomEditorTab([CanBeNull] Item item, [CanBeNull] ICollection<EditorTab> tabs)
        {
            // backwards compatibility
#pragma warning disable 618,612
            Assert.ArgumentNotNull(item, "item");
            Assert.ArgumentNotNull(tabs, "tabs");

            string customEditor = item.Appearance.CustomEditor;
#pragma warning restore 618,612

            if (string.IsNullOrEmpty(customEditor))
            {
                return;
            }

            customEditor = Uri.EscapeUriString(customEditor);
            EditorTab editorTab = GetEditorTab("CustomEditor", Translate.Text(Texts.CUSTOM_EDITOR), "Applications/16x16/form_blue.png", customEditor, false, "100");

            tabs.Add(editorTab);
        }

        /// <summary>
        /// Gets the custom editor tabs.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="tabs">The tabs.</param>
        static void GetCustomEditorTabs([NotNull] Item item, [NotNull] ICollection<EditorTab> tabs)
        {
            Assert.ArgumentNotNull(item, "item");
            Assert.ArgumentNotNull(tabs, "tabs");

            var list = new ListString(item["__Editors"]);

            var searchEditorId = new ID("{59F53BBB-D1F5-4E38-8EBA-0D73109BB59B}");

            foreach (string id in list)
            {
                var editor = Client.CoreDatabase.GetItem(id);

                if (editor == null)
                {
                    continue;
                }

                if (Context.Request != null && !string.IsNullOrWhiteSpace(Context.Request.QueryString["fo"]) && editor.ID == searchEditorId)
                {
                    continue;
                }

                if (editor == null)
                {
                    continue;
                }

                if (editor.ID == searchEditorId && !Settings.GetBoolSetting("BucketConfiguration.ItemBucketsEnabled", true))
                {
                    continue;
                }

                var url = new UrlString(editor["Url"]);
                url["id"] = item.ID.ToString();
                url["la"] = item.Language.ToString();
                url["language"] = item.Language.ToString();
                url["vs"] = item.Version.ToString();
                url["version"] = item.Version.ToString();

                var editorTab = GetEditorTab("T" + Sitecore.Data.ID.NewID.ToShortID(), editor["Header"], editor["Icon"], url.ToString(), editor["Refresh On Show"] == "1", editor["{00379E66-3C61-4296-A56E-67F531A2D8FB}"]);

                tabs.Add(editorTab);
            }
        }

        /// <summary>
        /// Gets the dynamic tabs.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="tabs">The tabs.</param>
        static void GetDynamicTabs([NotNull] Item item, [NotNull] ICollection<EditorTab> tabs)
        {
            Assert.ArgumentNotNull(item, "item");
            Assert.ArgumentNotNull(tabs, "tabs");

            var list = new ListString(WebUtil.GetFormValue("scEditorTabs"));

            foreach (var entry in list)
            {
                if (string.IsNullOrEmpty(entry))
                {
                    continue;
                }

                var parts = entry.Split('^');
                var commandName = parts[0];


                if (!string.IsNullOrEmpty(commandName))
                {
                    var command = CommandManager.GetCommand(commandName);
                    Assert.IsNotNull(command, typeof(Command), "Command \"{0}\" not found", parts[0]);

                    var context = new CommandContext(item);
                    var commandState = CommandManager.QueryState(command, context);
                    if (commandState == CommandState.Disabled || commandState == CommandState.Hidden)
                    {
                        continue;
                    }
                }

                var url = new UrlString(parts[3]);
                url["id"] = item.ID.ToString();
                url["la"] = item.Language.ToString();
                url["language"] = item.Language.ToString();
                url["vs"] = item.Version.ToString();
                url["version"] = item.Version.ToString();

                var editorTab = GetEditorTab(parts[5], parts[1], parts[2], url.ToString(), parts[4] == "1", "10000");

                editorTab.Closeable = true;

                tabs.Add(editorTab);
            }
        }

        /// <summary>
        /// Gets the editor tab.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="header">The header.</param>
        /// <param name="icon">The icon.</param>
        /// <param name="url">The URL.</param>
        /// <param name="refreshOnShow">if set to <c>true</c> this instance is refresh on show.</param>
        /// <param name="tabSortOrder"></param>
        /// <returns>The editor tab.</returns>
        [NotNull]
        static EditorTab GetEditorTab([NotNull] string id, [NotNull] string header, [NotNull] string icon, [NotNull] string url, bool refreshOnShow, string tabSortOrder)
        {
            Assert.ArgumentNotNull(id, "id");
            Assert.ArgumentNotNull(header, "header");
            Assert.ArgumentNotNull(icon, "icon");
            Assert.ArgumentNotNull(url, "url");

            long tabSortOrderResult;
            if (!long.TryParse(tabSortOrder, out tabSortOrderResult))
            {
                tabSortOrderResult = 100;
            }

            return new EditorTab { Header = header, Icon = icon, Url = url, RefreshOnShow = refreshOnShow, Id = id, TabSortOrder = tabSortOrderResult };
        }

        /// <summary>
        /// Gets the editor tabs.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="sections">The sections.</param>
        /// <returns>The editor tabs.</returns>
        [NotNull]
        static List<EditorTab> GetEditorTabs([NotNull] Item item, [NotNull] Sections sections)
        {
            Assert.ArgumentNotNull(item, "item");
            Assert.ArgumentNotNull(sections, "sections");

            var result = new List<EditorTab>();

            GetCustomEditorTab(item, result);

            GetCustomEditorTabs(item, result);

            GetContentTab(item, sections, result);

            if (Settings.GetBoolSetting("BucketConfiguration.ItemBucketsEnabled", true))
            {
                GetNewSearchTab(item, sections, result);
            }

            GetDynamicTabs(item, result);

            for (int i = 0; i < result.Count; i++)
            {
                result[i].TabSortOrder = result[i].TabSortOrder * 100 + i;
            }

            result.Sort((tab1, tab2) => tab1.TabSortOrder - tab2.TabSortOrder < 0 ? -1 : 1);

            return result;
        }

        /// <summary>
        /// Renders the content.
        /// </summary>
        /// <param name="parent">The parent.</param>
        /// <param name="tab">The tab.</param>
        /// <param name="active">if set to <c>true</c> this instance is active.</param>
        /// <param name="args">The editor args.</param>
        /// <returns>The content.</returns>
        /// <contract>
        /// 	<requires name="parent" condition="not null"/>
        /// 	<requires name="tab" condition="not null"/>
        /// 	<ensures condition="not null"/>
        /// </contract>
        [NotNull]
        static System.Web.UI.Control RenderContentEditor([NotNull] System.Web.UI.Control parent, [NotNull] EditorTab tab, bool active, RenderContentEditorArgs args)
        {
            Assert.ArgumentNotNull(parent, "parent");
            Assert.ArgumentNotNull(tab, "tab");
            Assert.ArgumentNotNull(args, "args");

            var grid = new Border { ID = "F" + tab.Id };
            parent.Controls.Add(grid);
            grid.Width = new Unit(100, UnitType.Percentage);
            grid.Height = new Unit(100, UnitType.Percentage);
            grid.Style.Add(HtmlTextWriterStyle.Position, "relative");

            if (!active)
            {
                grid.Style["display"] = "none";
            }

            // content panel
            var result = new HtmlGenericControl("div");
            grid.Controls.Add(result);

            result.Attributes["id"] = "EditorPanel";
            result.Attributes["class"] = "scEditorPanel";

            // validator panel
            if (UserOptions.ContentEditor.ShowValidatorBar)
            {
                var validator = new HtmlGenericControl("div");
                grid.Controls.Add(validator);

                validator.ID = "ValidatorPanel";
                validator.Attributes["class"] = "scValidatorPanel";
            }

            return result;
        }

        /// <summary>
        /// Renders the controls.
        /// </summary>
        /// <param name="output">The output.</param>
        /// <param name="item">The item.</param>
        /// <param name="sections">The sections.</param>
        

        /// <summary>
        /// Renders the editor tab controls.
        /// </summary>
        /// <param name="output">The output.</param>
        /// <param name="tab">The tab.</param>
        /// <param name="index">The index.</param>
        /// <param name="activeTab">The active tab.</param>
        static void RenderEditorTabControls([NotNull] HtmlTextWriter output, [NotNull] EditorTab tab, int index, int activeTab)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(tab, "tab");

            var controls = tab.Controls;
            if (string.IsNullOrEmpty(controls))
            {
                return;
            }

            var style = activeTab == index ? string.Empty : " style=\"display:none\"";

            output.Write("<div id=\"EditorTabControls_" + tab.Id + "\" class=\"scEditorTabControlsHolder\"" + style + ">");

            output.Write(tab.Controls);

            output.Write("</div>");
        }

        /// <summary>
        /// Renders the editor tab.
        /// </summary>
        /// <param name="output">The output.</param>
        /// <param name="tab">The tab.</param>
        /// <param name="index">The index.</param>
        /// <param name="count">The count.</param>
        /// <param name="activeTab">The active tab.</param>
        static void RenderEditorTab([NotNull] HtmlTextWriter output, [NotNull] EditorTab tab, int index, int count, int activeTab)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(tab, "tab");

            var active = index == activeTab;
            var tabClassName = active ? "scRibbonEditorTabActive" : "scRibbonEditorTabNormal";
            var headerClassName = active ? "scEditorTabHeaderActive" : "scEditorTabHeaderNormal";

            output.Write("<a id=\"B" + tab.Id + "\" href=\"#\" onclick=\"javascript:return scContent.onEditorTabClick(this,event,'" + tab.Id + "')\" class=\"" + tabClassName + "\">");

            var img = new ImageBuilder();

            if (index == 0)
            {
                img.Src = active ? "Images/Ribbon/tab0_h.png" : "Images/Ribbon/tab0.png";
                img.Class = "scEditorTabControlsTab0";

                output.Write(img.ToString());
            }

            output.Write("<span class=\"" + headerClassName + "\">");

            var image = new ImageBuilder { Src = StringUtil.GetString(tab.Icon, "Applications/16x16/form_blue.png"), Class = "scEditorTabIcon", Width = 16, Height = 16 };

            output.Write(image.ToString());

            output.Write("<span>" + tab.Header + "</span>");

            if (tab.Closeable)
            {
                image = new ImageBuilder
                {
                    Src = "Images/Close.png",
                    Class = "scEditorTabClose",
                    Width = 16,
                    Height = 16,
                    OnClick = "javascript:scContent.closeEditorTab('" + tab.Id + "');"
                };
                output.Write(image.ToString());
            }

            output.Write("</span>");

            if (index < count - 1)
            {
                if (activeTab == index + 1)
                {
                    img.Src = "Images/Ribbon/tab2_h2.png";
                }
                else
                {
                    img.Src = active ? "Images/Ribbon/tab2_h1.png" : "Images/Ribbon/tab2.png";
                }

                img.Class = "scEditorTabControlsTab2";

                output.Write(img.ToString());
            }
            else
            {
                img.Src = active ? "Images/Ribbon/tab3_h.png" : "Images/Ribbon/tab3.png";
                img.Class = "scEditorTabControlsTab3";

                output.Write(img.ToString());
            }

            output.Write("</a>");
        }

        /// <summary>
        /// Renders the tabs.
        /// </summary>
        /// <param name="args">The arguments.</param>
        /// <returns>The tabs.</returns>
        [NotNull]
        static System.Web.UI.Control RenderEditorTabs([NotNull] RenderContentEditorArgs args)
        {
            Assert.ArgumentNotNull(args, "args");

            if (args.EditorFormatter.IsFieldEditor)
            {
                var tab = new EditorTab { Id = "EmbeddedEditor" };
                return RenderContentEditor(args.Parent, tab, true, args);
            }

            if (!args.RenderTabsAndBars)
            {
                return Assert.ResultNotNull(args.Parent);
            }

            // tabs

            var item = args.Item;
            Assert.IsNotNull(item, "item");

            var sections = args.Sections;
            Assert.IsNotNull(sections, "sections");

            var editorTabs = GetEditorTabs(item, sections);

            var activeTabIndex = GetActiveTab(editorTabs);

            var output = new HtmlTextWriter(new StringWriter());
            output.Write("<div class='scEditorGrid'>");

            output.Write("<div id='EditorTabs'>");

            // RenderControls(output, item, sections);

            var count = editorTabs.Count;

            for (var n = 0; n < count; n++)
            {
                RenderEditorTabControls(output, editorTabs[n], n, activeTabIndex);
            }

            for (var n = 0; n < count; n++)
            {
                RenderEditorTab(output, editorTabs[n], n, count, activeTabIndex);
            }

            output.Write("</div>");

            args.Parent.Controls.Add(new LiteralControl(output.InnerWriter.ToString()));

            // frames
            var frames = new Border { ID = "EditorFrames" };

            System.Web.UI.Control result = frames;

            output = new HtmlTextWriter(new StringWriter());

            for (var n = 0; n < editorTabs.Count; n++)
            {
                var active = (n == activeTabIndex);

                var tab = editorTabs[n];

                if (tab.Url == "<content>")
                {
                    frames.Controls.Add(new LiteralControl(output.InnerWriter.ToString()));

                    result = RenderContentEditor(frames, tab, active, args);

                    output = new HtmlTextWriter(new StringWriter());
                }
                else
                {
                    if (string.Compare(tab.Id, AddSearchTabId, StringComparison.OrdinalIgnoreCase) == 0 && string.IsNullOrEmpty(tab.Url))
                    {
                        continue;
                    }

                    RenderFrame(output, item, tab, active, args.ReadOnly);
                }
            }

            frames.Controls.Add(new LiteralControl(output.InnerWriter.ToString()));

            args.Parent.Controls.Add(frames);
            args.Parent.Controls.Add(new LiteralControl("</div>"));

            return result;
        }

        /// <summary>
        /// Renders the frame.
        /// </summary>
        /// <param name="output">The output.</param>
        /// <param name="item">The item.</param>
        /// <param name="tab">The tab.</param>
        /// <param name="active">if set to <c>true</c> this instance is active.</param>
        /// <param name="readOnly">if set to <c>true</c> this instance is read only.</param>
        static void RenderFrame([NotNull] HtmlTextWriter output, [NotNull] Item item, [NotNull] EditorTab tab, bool active, bool readOnly)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(item, "item");
            Assert.ArgumentNotNull(tab, "tab");

            var url = new UrlString(UIUtil.GetUri(tab.Url));

            url.Append("id", item.ID.ToString());
            url.Append("language", item.Language.ToString());
            url.Append("version", item.Version.ToString());
            url.Append("database", item.Database.Name);
            url.Append("readonly", readOnly ? "1" : "0");

            url.Append("la", item.Language.ToString());
            url.Append("vs", item.Version.ToString());
            url.Append("db", item.Database.Name);

            string style = active ? string.Empty : "display:none";

            if (!string.IsNullOrEmpty(style))
            {
                style = " style=\"" + style + "\"";
            }

            output.Write("<iframe id=\"F" + tab.Id + "\" src=\"" + url + "\" width=\"100%\" height=\"100%\" frameborder=\"no\" marginwidth=\"0\" marginheight=\"0\"" + style + "></iframe>");
        }

        #endregion

        #region Nested type: EditorTab

        class EditorTab
        {
            #region Fields

            string _header;
            string _icon;
            string _id;
            string _url;
            string _controls;

            #endregion

            #region Public properties

            /// <summary>
            /// Gets or sets a value indicating whether this <see cref="EditorTab"/> is closeable.
            /// </summary>
            /// <value><c>true</c> if closeable; otherwise, <c>false</c>.</value>
            public bool Closeable { get; set; }

            /// <summary>
            /// Gets or sets the controls.
            /// </summary>
            /// <value>The controls.</value>
            [NotNull]
            public string Controls
            {
                get
                {
                    return _controls ?? string.Empty;
                }
                set
                {
                    Assert.ArgumentNotNullOrEmpty(value, "value");

                    _controls = value;
                }
            }
            /// <summary>
            /// Gets or sets the header.
            /// </summary>
            /// <value>The header.</value>
            [CanBeNull]
            public string Header
            {
                get
                {
                    return _header;
                }
                set
                {
                    Assert.ArgumentNotNull(value, "value");

                    _header = value;
                }
            }

            /// <summary>
            /// Gets or sets the icon.
            /// </summary>
            /// <value>The icon.</value>
            [CanBeNull]
            public string Icon
            {
                get
                {
                    return _icon;
                }
                set
                {
                    Assert.ArgumentNotNull(value, "value");
                    _icon = value;
                }
            }

            /// <summary>
            /// Gets or sets the id.
            /// </summary>
            /// <value>The id.</value>
            [CanBeNull]
            public string Id
            {
                get
                {
                    return _id;
                }
                set
                {
                    Assert.ArgumentNotNull(value, "value");
                    _id = value;
                }
            }

            /// <summary>
            /// Gets or sets a value indicating whether the <see cref="EditorTab"/> refreshs the on show.
            /// </summary>
            /// <value>
            /// 	<c>true</c> if the <see cref="EditorTab"/> refreshs the  on show; otherwise, <c>false</c>.
            /// </value>
            public bool RefreshOnShow { get; set; }

            /// <summary>
            /// Gets or sets the URL.
            /// </summary>
            /// <value>The URL.</value>
            [CanBeNull]
            public string Url
            {
                get
                {
                    return _url;
                }
                set
                {
                    Assert.ArgumentNotNull(value, "value");
                    _url = value;
                }
            }

            public long TabSortOrder { get; set; }

            #endregion
        }

        #endregion
    }
}
