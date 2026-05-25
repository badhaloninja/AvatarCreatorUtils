using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using FrooxEngine.FinalIK;
using FrooxEngine.CommonAvatar;
using FrooxEngine.UIX;
using Elements.Core;

namespace AvatarCreatorUtils
{
    public class AvatarCreatorUtils : ResoniteMod
    {
        public override string Name => PluginMetadata.NAME;
        public override string Author => PluginMetadata.AUTHORS;
        public override string Version => PluginMetadata.VERSION;
        public override string Link => PluginMetadata.REPOSITORY_URL;

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> GroupProxies = new("group_proxies", $"Settings.{PluginMetadata.GUID}.GroupProxies.Description", () => true);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> AddVariableSpace = new("add_avatar_variable_space", $"Settings.{PluginMetadata.GUID}.AddVariableSpace.Description", () => true);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<string> AvatarVariableSpaceName = new("avatar_variable_space_name", $"Settings.{PluginMetadata.GUID}.AvatarVariableSpaceName.Description", () => "Avatar");

        public override void OnEngineInit()
        {
            Harmony harmony = new(PluginMetadata.GUID);
            harmony.PatchAll();
        }

        [HarmonyPatch]
        sealed class Patches
        {
            public static WeakReference<AvatarCreator> avatarCreatorRef = new(null);

            [HarmonyPostfix]
            [HarmonyPatch(typeof(VRIKAvatar), "EnsurePoseNode")]
            public static void CleanupProxies(VRIKAvatar __instance, AvatarPoseNode __result)
            {
                if (!GroupProxies.Value) return;
                __result.Slot.Parent = __instance.Slot.FindChildOrAdd("Proxies");
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(AvatarCreator), "RunCreate")]
            public static void GetCreatorRef(AvatarCreator __instance)
            {
                avatarCreatorRef.SetTarget(__instance);
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(AvatarCreator), "EnsureHeadPositioner")]
            public static void InjectStuff(Slot root)
            {
                avatarCreatorRef.TryGetTarget(out AvatarCreator instance);
                if (instance == null) return;

                if (AddVariableSpace.Value)
                {
                    root.GetComponentOrAttach<DynamicVariableSpace>().SpaceName.Value = AvatarVariableSpaceName.Value;
                }

                if (TryReadDynamicValue(instance.Slot, "AvatarCreator/AvatarName", out string avatarName) && avatarName != null)
                { // If AvatarName is set rename avatar root
                    root.Name = avatarName;
                }

                SetupAbout(instance.Slot, root);
                avatarCreatorRef.SetTarget(null);
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(AvatarCreator), "OnAttach")]
            public static void AppendAvatarCreator(AvatarCreator __instance)
            {
                var canvas = __instance.Slot.GetComponentInChildren<Canvas>();
                var verticalLayout = canvas?.Slot?.GetComponentInChildren<VerticalLayout>();

                if (verticalLayout == null) return;

                // Append the height of our new fields to the canvas' size
                // Also make the canvas slightly wider
                canvas.Size.Value += new float2(40, 216);

                var ui = new UIBuilder(verticalLayout.Slot);
                RadiantUI_Constants.SetupEditorStyle(ui, false);

                __instance.Slot.AttachComponent<DynamicVariableSpace>().SpaceName.Value = "AvatarCreator";
                var data = __instance.Slot.AddSlot("Data");

                ui.Style.MinHeight = 24f;
                ui.Text("Avatar Info:");

                VariableEditorField<string>("AvatarCreator/AvatarName", "Avatar Name", data, ui);
                VariableEditorField<string>("AvatarCreator/Link", "Avatar Link", data, ui);
                VariableEditorField<string>("AvatarCreator/VersionText", "Version Text", data, ui);

                ui.Style.MinHeight = 96f;
                var thumbnail = data.AttachComponent<AssetLoader<ITexture2D>>();
                thumbnail.Asset.SyncWithVariable("AvatarCreator/Thumbnail");
                SyncMemberEditorBuilder.Build(thumbnail.Asset, "Avatar Thumbnail", null, ui);
            }
        }
        private static void VariableEditorField<T>(string name, string label, Slot data, UIBuilder ui) {
            var variable = data.AttachComponent<DynamicValueVariable<T>>();
            variable.VariableName.Value = name;
            SyncMemberEditorBuilder.Build(variable.Value, label, null, ui);
        }

        private static void SetupAbout(Slot data, Slot root)
        {
            TryAddComment(data, root, "AvatarCreator/Link");
            TryAddComment(data, root, "AvatarCreator/VersionText");

            if (TryReadDynamicValue(data, "AvatarCreator/Thumbnail", out IAssetProvider<ITexture2D> thumbnail) && thumbnail != null)
            {
                var t2dAsset = thumbnail as IAssetProvider<Texture2D>;
                var assetLoader = root.FindChildOrAdd("About").AttachComponent<AssetLoader<Texture2D>>();
                assetLoader.Asset.Target = t2dAsset;

                var thumbnailSource = root.AttachComponent<ItemTextureThumbnailSource>();


                if (AddVariableSpace.Value)
                {
                    var variableSpace = AvatarVariableSpaceName.Value;
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

            Slot about = root.FindChild("About");
            about?.OrderOffset = -10;
        }

        private static void TryAddComment(Slot avatarCreatorData, Slot AvatarRoot, string variableName)
        {
            if (TryReadDynamicValue(avatarCreatorData, variableName, out string value) && value != null)
            {
                AvatarRoot.FindChildOrAdd("About").AttachComponent<Comment>().Text.Value = value;
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