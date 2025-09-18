using MultiplayerAssistant.Config;
using StardewValley;
using StardewValley.Menus;
using System.Reflection;

namespace MultiplayerAssistant.HostAutomatorStages
{
    internal class ProcessDialogueBehaviorLink : BehaviorLink
    {
        private static FieldInfo textBoxFieldInfo = typeof(NamingMenu).GetField("textBox", BindingFlags.NonPublic | BindingFlags.Instance);

        private ModConfig config;

        public ProcessDialogueBehaviorLink(ModConfig config, BehaviorLink next = null) : base(next)
        {
            this.config = config;
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
                        db.receiveLeftClick(0, 0); // Skip the non-question dialogue
                        state.SkipDialogue();
                    }
                    else
                    {
                        // 中文说明：1.6 下 DialogueBox 的 responses 访问方式变化，这里通过反射读取以保持兼容
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
                            if (config.MushroomsOrBats.ToLower() == "mushrooms")
                                db.selectedResponse = mushroomsResponseIdx;
                            else if (config.MushroomsOrBats.ToLower() == "bats")
                                db.selectedResponse = batsResponseIdx;
                        }
                        else if (yesResponseIdx >= 0 && noResponseIdx >= 0)
                        {
                            db.selectedResponse = config.AcceptPet ? yesResponseIdx : noResponseIdx;
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
                        TextBox textBox = (TextBox) textBoxFieldInfo.GetValue(nm);
                        textBox.Text = config.PetName;
                        textBox.RecieveCommandInput('\r');
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
