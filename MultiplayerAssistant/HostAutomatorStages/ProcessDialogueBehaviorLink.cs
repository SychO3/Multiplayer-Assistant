using MultiplayerAssistant.Config;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using System.Collections.Generic;
using System.Reflection;

namespace MultiplayerAssistant.HostAutomatorStages
{
    /// <summary>
    /// 处理游戏对话框的行为链节点，自动回答对话框中的问题
    /// </summary>
    internal class ProcessDialogueBehaviorLink : BehaviorLink
    {
        private static FieldInfo textBoxFieldInfo = typeof(NamingMenu).GetField("textBox", BindingFlags.NonPublic | BindingFlags.Instance);
        private readonly IMonitor monitor;
        private ModConfig config;

        public ProcessDialogueBehaviorLink(IMonitor monitor, ModConfig config, BehaviorLink next = null) : base(next)
        {
            this.monitor = monitor;
            this.config = config;
            // 中文说明：初始化对话处理器，用于自动回答游戏中的对话选项
            monitor.Debug("初始化 ProcessDialogueBehaviorLink", nameof(ProcessDialogueBehaviorLink));
        }

        public override void Process(BehaviorState state)
        {
            if (Game1.activeClickableMenu != null)
            {
                if (Game1.activeClickableMenu is DialogueBox db)
                {
                    if (state.HasBetweenDialoguesWaitTicks())
                    {
                        state.DecrementBetweenDialoguesWaitTicks();
                    }
                    else if (!db.isQuestion)
                    {
                        // 中文说明：跳过非问题对话
                        monitor.Debug("跳过非问题对话", nameof(ProcessDialogueBehaviorLink));
                        db.receiveLeftClick(0, 0); // Skip the non-question dialogue
                        state.SkipDialogue();
                    }
                    else
                    {
                        // 中文说明：1.6 下 DialogueBox 的 responses 访问方式变化，这里通过反射读取以保持兼容
                        monitor.Debug("处理问题对话", nameof(ProcessDialogueBehaviorLink));
                        var responsesMember = typeof(DialogueBox).GetField("responses", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                                ?? (MemberInfo)typeof(DialogueBox).GetProperty("responses", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        List<Response> responses = null;
                        if (responsesMember is FieldInfo fi)
                        {
                            responses = fi.GetValue(db) as List<Response>;
                        }
                        else if (responsesMember is PropertyInfo pi)
                        {
                            responses = pi.GetValue(db) as List<Response>;
                        }

                        int mushroomsResponseIdx = -1;
                        int batsResponseIdx = -1;
                        int yesResponseIdx = -1;
                        int noResponseIdx = -1;
                        if (responses != null)
                        {
                            for (int i = 0; i < responses.Count; i++)
                            {
                                var response = responses[i];
                                var lowercaseText = response.responseText.ToLower();
                                if (lowercaseText == "mushrooms")
                                {
                                    mushroomsResponseIdx = i;
                                }
                                else if (lowercaseText == "bats")
                                {
                                    batsResponseIdx = i;
                                }
                                else if (lowercaseText == "yes")
                                {
                                    yesResponseIdx = i;
                                }
                                else if (lowercaseText == "no")
                                {
                                    noResponseIdx = i;
                                }
                            }
                        }

                        db.selectedResponse = 0;
                        if (mushroomsResponseIdx >= 0 && batsResponseIdx >= 0)
                        {
                            // 中文说明：检测到蘑菇/蝙蝠洞穴选择对话
                            if (config.MushroomsOrBats.ToLower() == "mushrooms")
                            {
                                db.selectedResponse = mushroomsResponseIdx;
                                monitor.Info($"自动选择：蘑菇洞穴（配置：{config.MushroomsOrBats}）", nameof(ProcessDialogueBehaviorLink));
                            }
                            else if (config.MushroomsOrBats.ToLower() == "bats")
                            {
                                db.selectedResponse = batsResponseIdx;
                                monitor.Info($"自动选择：蝙蝠洞穴（配置：{config.MushroomsOrBats}）", nameof(ProcessDialogueBehaviorLink));
                            }
                        }
                        else if (yesResponseIdx >= 0 && noResponseIdx >= 0)
                        {
                            // 中文说明：检测到是/否选择对话（通常是宠物选择）
                            db.selectedResponse = config.AcceptPet ? yesResponseIdx : noResponseIdx;
                            monitor.Info($"自动选择：{(config.AcceptPet ? "是" : "否")}（宠物选择）", nameof(ProcessDialogueBehaviorLink));
                        }

                        db.receiveLeftClick(0, 0);
                        state.SkipDialogue();
                    }
                }
                else if (Game1.activeClickableMenu is NamingMenu nm)
                {
                    if (state.HasBetweenDialoguesWaitTicks())
                    {
                        state.DecrementBetweenDialoguesWaitTicks();
                    }
                    else
                    {
                        // 中文说明：处理命名菜单（宠物命名）
                        TextBox textBox = (TextBox) textBoxFieldInfo.GetValue(nm);
                        textBox.Text = config.PetName;
                        textBox.RecieveCommandInput('\r');
                        monitor.Info($"自动输入宠物名称：{config.PetName}", nameof(ProcessDialogueBehaviorLink));
                        state.SkipDialogue();
                    }
                }
                else if (Game1.activeClickableMenu is LevelUpMenu lum) 
                {
                    if (state.HasBetweenDialoguesWaitTicks())
                    {
                        state.DecrementBetweenDialoguesWaitTicks();
                    }
                    else
                    {
                        // 中文说明：自动关闭升级菜单
                        monitor.Debug("自动关闭升级菜单", nameof(ProcessDialogueBehaviorLink));
                        lum.okButtonClicked();
                    }
                }
                else
                {
                    state.ClearBetweenDialoguesWaitTicks();
                    processNext(state);
                }
            }
            else
            {
                state.ClearBetweenDialoguesWaitTicks();
                processNext(state);
            }
        }
    }
}
