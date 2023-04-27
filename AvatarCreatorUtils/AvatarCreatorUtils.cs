using FrooxEngine;
using HarmonyLib;
using NeosModLoader;
using System;
using FrooxEngine.FinalIK;
using FrooxEngine.CommonAvatar;
using FrooxEngine.UIX;
using BaseX;
using FrooxEngine.LogiX.WorldModel;

namespace AvatarCreatorUtils
{
    public class AvatarCreatorUtils : NeosMod
    {
        public override string Name => "AvatarCreatorUtils";
        public override string Author => "badhaloninja";
        public override string Version => "1.0.0";
        public override string Link => "https://github.com/badhaloninja/AvatarCreatorUtils";

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> GroupProxies = new ModConfigurationKey<bool>("group_proxies", "Put proxies under a 'Proxies' root", () => true);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> AddVariableSpace = new ModConfigurationKey<bool>("add_avatar_variable_space", "Add an Avatar variable space", () => true);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<string> AvatarVariableSpaceName = new ModConfigurationKey<string>("avatar_variable_space_name", "Avatar variable space name", () => "Avatar");



        private static ModConfiguration config;
        public override void OnEngineInit()
        {
            Harmony harmony = new Harmony("me.badhaloninja.AvatarCreatorUtils");
            harmony.PatchAll();

            config = GetConfiguration();
        }
        [HarmonyPatch]
        class Patches
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(VRIKAvatar), "EnsurePoseNode")]
            public static void CleanupProxies(VRIKAvatar __instance, AvatarPoseNode __result)
            {
                if (!config.GetValue(GroupProxies)) return;
                __result.Slot.Parent = __instance.Slot.FindOrAdd("Proxies");
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(AvatarCreator), "EnsureHeadPositioner")]
            public static void InjectStuff(AvatarCreator __instance, Slot root)
            {
                if (config.GetValue(AddVariableSpace))
                {
                    root.GetComponentOrAttach<DynamicVariableSpace>().SpaceName.Value = config.GetValue(AvatarVariableSpaceName);
                }

                if (TryReadDynamicValue(__instance.Slot, "AvatarCreator/AvatarName", out string avatarName) && avatarName != null)
                { // If AvatarName is set rename avatar root
                    root.Name = avatarName;
                }

                SetupAbout(__instance.Slot, root);
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(AvatarCreator), "OnAttach")]
            public static void AppendAvatarCreator(AvatarCreator __instance)
            {
                var panel = __instance.Slot.GetComponentInChildren<NeosCanvasPanel>();
                if (panel?.Canvas == null) return;

                panel.CanvasSize = new float2(300, 640);

                var ui = new UIBuilder(panel.Canvas);

                __instance.Slot.AttachComponent<DynamicVariableSpace>().SpaceName.Value = "AvatarCreator";
                var data = __instance.Slot.AddSlot("Data");

                //Slot.CreateVariable<T> does not return the variable component
                var name = data.AttachComponent<DynamicValueVariable<string>>();
                var link = data.AttachComponent<DynamicValueVariable<string>>();
                var versionText = data.AttachComponent<DynamicValueVariable<string>>();

                name.VariableName.Value = "AvatarCreator/AvatarName";
                link.VariableName.Value = "AvatarCreator/Link";
                versionText.VariableName.Value = "AvatarCreator/VerionText";

                var thumbnail = data.AttachComponent<AssetLoader<ITexture2D>>();
                thumbnail.Asset.SyncWithVariable("AvatarCreator/Thumbnail");

                ui.NestInto(ui.Root[0]);

                ui.Style.MinHeight = 24f;
                ui.Text("Avatar Info:");
                SyncMemberEditorBuilder.Build(name.Value, "Avatar Name", null, ui);
                SyncMemberEditorBuilder.Build(link.Value, "Avatar Link", null, ui);
                SyncMemberEditorBuilder.Build(versionText.Value, "Version Text", null, ui);

                ui.Style.MinHeight = 96f;
                SyncMemberEditorBuilder.Build(thumbnail.Asset, "Avatar Thumbnail", null, ui);

            }
        }


        private static void SetupAbout(Slot data, Slot root)
        {
            TryAddComment(data, root, "AvatarCreator/Link");
            TryAddComment(data, root, "AvatarCreator/VerionText");

            if (TryReadDynamicValue(data, "AvatarCreator/Thumbnail", out IAssetProvider<ITexture2D> thumbnail) && thumbnail != null)
            {
                var t2dAsset = thumbnail as IAssetProvider<Texture2D>;
                var assetLoader = root.FindOrAdd("About").AttachComponent<AssetLoader<Texture2D>>();
                assetLoader.Asset.Target = t2dAsset;

                var thumbnailSource = root.AttachComponent<ItemTextureThumbnailSource>();


                if (config.GetValue(AddVariableSpace))
                {
                    var variableSpace = config.GetValue(AvatarVariableSpaceName);
                    var variablePrefix = string.IsNullOrEmpty(variableSpace) ? "" : variableSpace + "/";
                    var thumbnailVariable = variablePrefix + "Thumbnail";

                    assetLoader.Asset.SyncWithVariable(thumbnailVariable);
                    thumbnailSource.Texture.SyncWithVariable(thumbnailVariable);

                }
                else
                {
                    thumbnailSource.Texture.DriveFrom(assetLoader.Asset, true);
                }
            }

            Slot about = root.Find("About");
            if (about != null) about.OrderOffset = -10;
        }

        private static void TryAddComment(Slot avatarCreatorData, Slot AvatarRoot, string variableName)
        {
            if (TryReadDynamicValue(avatarCreatorData, variableName, out string value) && value != null)
            {
                AvatarRoot.FindOrAdd("About").AttachComponent<Comment>().Text.Value = value;
            }
        }

        public static bool TryReadDynamicValue<T>(Slot root, string name, out T value)
        {
            value = Coder<T>.Default;
            DynamicVariableHelper.ParsePath(name, out string spaceName, out string text);

            if (string.IsNullOrEmpty(text)) return false;

            DynamicVariableSpace dynamicVariableSpace = root.FindSpace(spaceName);
            if (dynamicVariableSpace == null) return false;
            return dynamicVariableSpace.TryReadValue(text, out value);
        }
    }

}