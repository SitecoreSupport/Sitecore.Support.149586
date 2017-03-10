using Sitecore.Analytics.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Resources;
using Sitecore.Resources.Media;
using Sitecore.Shell.Framework.CommandBuilders;
using Sitecore.Web.UI;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Sitecore.Support.Analytics.Data
{
   public delegate IEnumerable<ContentProfile> GetProfilesDelegate(Item item, out TrackingField trackingField);

    public static class ProfileUtil
    {
        internal static class UI
        {
            public const string DefaultPresetThumbnail = "Custom/32x32/profilecard_thumbnail.png";
            public const string DefaultProfileThumbnail = "Custom/32x32/profile_customized.png";
            public static string GetPersonalizeProfileCommand(Item contextItem, Item profileItem)
            {
                Assert.ArgumentNotNull(contextItem, "contextItem");
                Assert.ArgumentNotNull(profileItem, "profileItem");
                bool flag2;
                bool flag = Sitecore.Analytics.Data.ProfileUtil.IsMultiplePreset(profileItem, out flag2);
                CommandBuilder commandBuilder = new CommandBuilder("item:personalizeprofile");
                commandBuilder.Add("profileid", profileItem.ID.ToShortID().ToString());
                commandBuilder.Add("multiple", flag ? "1" : "0");
                commandBuilder.Add("supportweights", flag2 ? "1" : "0");
                commandBuilder.Add("language", (Context.Language == null) ? contextItem.Language.Name : Context.Language.ToString());
                return commandBuilder.ToString();
            }
            public static string GetPresetThumbnail(Item presetItem)
            {
                Assert.ArgumentNotNull(presetItem, "presetItem");
                string text = string.Empty;
                Field field = presetItem.Fields["Image"];
                if (field != null)
                {
                    ImageField imageField = new ImageField(field);
                    Item mediaItem = imageField.MediaItem;
                    if (mediaItem != null)
                    {
                        MediaUrlOptions options = new MediaUrlOptions
                        {
                            MaxHeight = 32,
                            MaxWidth = 32,
                            Database = mediaItem.Database
                        };
                        text = MediaManager.GetMediaUrl(new MediaItem(mediaItem), options);
                    }
                }
                if (string.IsNullOrEmpty(text))
                {
                    text = "Custom/32x32/profilecard_thumbnail.png";
                }
                return Images.GetThemedImageSource(text, ImageDimension.id32x32);
            }
            public static string GetProfileThumbnail(Item profileItem)
            {
                Assert.ArgumentNotNull(profileItem, "profileItem");
                return Images.GetThemedImageSource("Custom/32x32/profile_customized.png", ImageDimension.id32x32);
            }
        }

        public static GetProfilesDelegate GetProfiles;
        static ProfileUtil()
        {
            ProfileUtil.GetProfiles = (GetProfilesDelegate)Delegate.CreateDelegate(typeof(GetProfilesDelegate), typeof(ProfileUtil).GetMethod("GetProfiles", BindingFlags.Static | BindingFlags.NonPublic, Type.DefaultBinder, new Type[]
            {
                typeof(Item),
                typeof(TrackingField)
            }, null));
        }
    }
}
