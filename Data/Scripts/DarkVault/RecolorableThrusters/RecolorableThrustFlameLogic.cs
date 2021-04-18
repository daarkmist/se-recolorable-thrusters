using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Components;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.Models;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using VRageRender;
using VRageRender.Import;

namespace DarkVault.RecolorableThrusters
{

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Thrust), false)]
    public class RecolorableThrustFlameLogic : MyGameLogicComponent
    {

        private static readonly string _CUSTOM_DATA_SECTION = "FlameColors";

        private enum RenderMode
        {
            Linked, Blended, Separate
        }

        public Vector4 FlameIdleColor
        {
            get { return m_flameIdleColor; }
            set
            {
                if (!m_flameColorsLocked)
                {
                    m_flameIdleColor = value;

                    UpdateCustomData();
                }
            }
        }

        public Vector4 FlameFullColor
        {
            get { return m_flameFullColor; }
            set
            {
                if (!m_flameColorsLocked)
                {
                    m_flameFullColor = value;

                    UpdateCustomData();
                }
            }
        }

        public bool HideThrustFlames
        {
            get { return m_hideFlames; }
            set
            {
                if (!m_flameColorsLocked)
                {
                    m_hideFlames = value;

                    UpdateCustomData();
                }
            }
        }

        public bool HasFlames
        {
            get
            {
                if (!m_hasFlames.HasValue)
                    CheckFlameDummies();
                
                return m_hasFlames ?? true;
            }
        }

        private static List<IMyTerminalControl> m_customControls = new List<IMyTerminalControl>();

        private Vector4 m_flameIdleColor;
        private Vector4 m_flameFullColor;
        private bool m_flameColorsLocked;
        private RenderMode m_renderMode = RenderMode.Linked;
        private bool m_hideFlames = false;
        private IMyThrust m_thruster;
        private bool m_initialized = false;
        private bool? m_hasFlames = null;
        private float m_buildRatio;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            m_thruster = Entity as IMyThrust;

            var blockDefinition = m_thruster.SlimBlock.BlockDefinition as MyThrustDefinition;

            m_flameIdleColor = blockDefinition.FlameIdleColor;
            m_flameFullColor = blockDefinition.FlameFullColor;
            
            lock (m_customControls)
            {
                if (m_customControls.Count == 0)
                    CreateTerminalControls();
            }

            OnCustomDataChanged(m_thruster);

            m_buildRatio = m_thruster.SlimBlock.BuildLevelRatio;
            m_thruster.CustomDataChanged += OnCustomDataChanged;
            m_thruster.CubeGrid.OnBlockIntegrityChanged += OnIntegrityChanged;

            NeedsUpdate |= (MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME);

            m_initialized = true;
        }

        public override void UpdateOnceBeforeFrame()
        {
            CheckFlameDummies();
            UpdateFlames();
        }

        public override void MarkForClose()
        {
            m_thruster.CustomDataChanged -= OnCustomDataChanged;
            m_thruster.CubeGrid.OnBlockIntegrityChanged -= OnIntegrityChanged;
        }

        public void OnCustomDataChanged(IMyTerminalBlock block)
        {
            var updateSettings = false;
            var settings = new Dictionary<string, List<string>>();
            ParseCustomData(ref settings);

            if (settings.ContainsKey(_CUSTOM_DATA_SECTION))
            {
                var lines = settings[_CUSTOM_DATA_SECTION];

                if (lines != null)
                {
                    m_flameIdleColor = ParseVector(lines[0], m_flameIdleColor);
                    m_flameFullColor = ParseVector(lines[1], m_flameFullColor);
                    
                    if (lines.Count > 2)
                        m_flameColorsLocked = bool.Parse(lines[2]);

                    if (lines.Count > 3)
                    {
                        bool oldLinkedValue;
                        if (bool.TryParse(lines[3], out oldLinkedValue))
                        {
                            m_renderMode = RenderMode.Linked;
                            updateSettings = true;
                        } 
                        else
                        {
                            m_renderMode = (RenderMode)Enum.Parse(typeof(RenderMode), lines[3]);
                        }
                    }

                    if (lines.Count > 4)
                    {
                        m_hideFlames = bool.Parse(lines[4]);
                    }
                }

                if (updateSettings)
                    UpdateCustomData();

                if (m_renderMode == RenderMode.Linked)
                    m_flameFullColor = m_flameIdleColor;

                if (m_initialized)
                {
                    foreach (var control in m_customControls)
                    {
                        control.UpdateVisual();
                    }
                }

                UpdateFlames();
            }
        }

        public override void UpdateAfterSimulation100()
        {
            UpdateFlames();
        }

        public void OnIntegrityChanged(IMySlimBlock block)
        {
            if (block.FatBlock != m_thruster)
                return;

            var thrust = m_thruster as MyThrust;
            var blockDefinition = thrust.BlockDefinition;

            if (blockDefinition.ModelChangeIsNeeded(m_buildRatio, block.BuildLevelRatio) ||
                blockDefinition.ModelChangeIsNeeded(block.BuildLevelRatio, m_buildRatio))
            {
                CheckFlameDummies();
                UpdateFlames();
            }

            m_buildRatio = block.BuildLevelRatio;
        }

        private void CreateTerminalControls()
        {
            // Just to make sure we're not subscribing twice without using locks

            MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControlGetter;
            MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlGetter;

            IMyTerminalControlCheckbox checkbox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyThrust>("LockFlameColors");

            checkbox.Title = MyStringId.GetOrCompute("Lock Flame Colors");
            checkbox.Getter = (block) =>
            {
                if (block == null || block.GameLogic == null)
                    return false;

                var logic = block.GameLogic.GetAs<RecolorableThrustFlameLogic>();

                return logic != null ? logic.m_flameColorsLocked : false;
            };

            checkbox.Setter = (block, value) =>
            {
                var logic = block.GameLogic.GetAs<RecolorableThrustFlameLogic>();

                if (logic != null)
                {
                    logic.m_flameColorsLocked = value;

                    logic.UpdateFlames();
                    logic.UpdateCustomData();
                }
            };

            checkbox.SupportsMultipleBlocks = true;

            m_customControls.Add(checkbox);

            // FlameIdleColor

            IMyTerminalControlColor color = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlColor, IMyThrust>("FlameIdleColor");

            color.Title = MyStringId.GetOrCompute("Idle");

            color.Getter = (block) =>
            {
                if (block == null || block.GameLogic == null)
                    return (Vector4)Color.Black;

                var logic = block.GameLogic.GetAs<RecolorableThrustFlameLogic>();
                return logic != null ? logic.m_flameIdleColor : (Vector4)Color.Black; 
            };

            color.Setter = (block, value) =>
            {
                var logic = block.GameLogic.GetAs<RecolorableThrustFlameLogic>();

                if (logic != null)
                {
                    logic.m_flameIdleColor = value.ToVector4();
                    logic.m_flameIdleColor.W = 0.75f;

                    if (logic.m_renderMode == RenderMode.Linked)
                        logic.m_flameFullColor = logic.m_flameIdleColor;

                    foreach (var control in m_customControls)
                    {
                        if (control.Id == "FlameFullColor")
                            control.UpdateVisual();
                    }

                    logic.UpdateFlames();
                    logic.UpdateCustomData();
                }
            };

            m_customControls.Add(color);

            var propertyIC = MyAPIGateway.TerminalControls.CreateProperty<Color, IMyThrust>("FlameIdleColorOverride");
                  
            propertyIC.SupportsMultipleBlocks = false;
            propertyIC.Getter = (block) =>
            {
                if (block == null || block.GameLogic == null)
                    return Vector4.Zero;

                var logic = block.GameLogic.GetAs<RecolorableThrustFlameLogic>();
                return logic != null ? (Color)logic.FlameIdleColor : Color.Transparent;
            };

            propertyIC.Setter = (block, value) =>
            {
                if (block == null || block.GameLogic == null)
                    return;

                var logic = block.GameLogic.GetAs<RecolorableThrustFlameLogic>();

                if (logic != null)
                {
                    logic.m_flameIdleColor = value.ToVector4();
                    logic.m_flameIdleColor.W = 0.75f;
                    
                    if (logic.m_renderMode == RenderMode.Linked)
                        logic.FlameFullColor = logic.FlameIdleColor;
                }
            };

            MyAPIGateway.TerminalControls.AddControl<IMyThrust>(propertyIC);

            // FlameFullColor

            color = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlColor, IMyThrust>("FlameFullColor");

            color.Title = MyStringId.GetOrCompute("Full");
            color.Enabled = (block) =>
            {
                if (block == null || block.GameLogic == null)
                    return false;

                var logic = block.GameLogic.GetAs<RecolorableThrustFlameLogic>();
                return logic != null ? (logic.m_renderMode != RenderMode.Linked) : false; 
            };

            color.Getter = (block) =>
            {
                if (block == null || block.GameLogic == null)
                    return (Vector4)Color.Black;

                var logic = block.GameLogic.GetAs<RecolorableThrustFlameLogic>();
                return logic != null ? logic.m_flameFullColor : (Vector4)Color.Black; 
            };

            color.Setter = (block, value) =>
            {
                var logic = block.GameLogic.GetAs<RecolorableThrustFlameLogic>();

                if (logic != null)
                {
                    logic.m_flameFullColor = value.ToVector4();
                    logic.m_flameFullColor.W = 0.75f;

                    logic.UpdateFlames();
                    logic.UpdateCustomData();
                }
            };

            m_customControls.Add(color);

            var propertyFC = MyAPIGateway.TerminalControls.CreateProperty<Color, IMyThrust>("FlameFullColorOverride");
                  
            propertyFC.SupportsMultipleBlocks = false;
            propertyFC.Getter = (block) =>
            {
                if (block == null || block.GameLogic == null)
                    return Vector4.Zero;

                var logic = block.GameLogic.GetAs<RecolorableThrustFlameLogic>();
                return logic != null ? (Color)logic.FlameFullColor : Color.Transparent;
            };

            propertyFC.Setter = (block, value) =>
            {
                if (block == null || block.GameLogic == null)
                    return;

                var logic = block.GameLogic.GetAs<RecolorableThrustFlameLogic>();
                
                if (logic != null && logic.m_renderMode != RenderMode.Linked)
                {
                    logic.m_flameFullColor = value.ToVector4();
                    logic.m_flameFullColor.W = 0.75f;
                }
            };

            MyAPIGateway.TerminalControls.AddControl<IMyThrust>(propertyFC);

            IMyTerminalControlSlider renderMode = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyThrust>("FlameRenderMode");

            renderMode.Title = MyStringId.GetOrCompute("Flame Render Mode");
            renderMode.SetLimits(0, 2);
            renderMode.Getter = (block) =>
            {
                if (block == null || block.GameLogic == null)
                    return (int)m_renderMode;

                var logic = block.GameLogic.GetAs<RecolorableThrustFlameLogic>();

                return logic != null ? (int)logic.m_renderMode : 0;
            };

            renderMode.Setter = (block, value) =>
            {
                if (block == null || block.GameLogic == null)
                    return;

                var logic = block.GameLogic.GetAs<RecolorableThrustFlameLogic>();

                if (logic != null)
                {
                    logic.m_renderMode = (RenderMode)value;

                    if (logic.m_renderMode == RenderMode.Linked)
                        logic.m_flameFullColor = logic.m_flameIdleColor;

                    logic.UpdateFlames();
                    logic.UpdateCustomData();

                    foreach (var control in m_customControls)
                    {
                        if (control.Id == "FlameFullColor")
                            control.UpdateVisual();
                    }
                }
            };

            renderMode.Writer = (block, sb) =>
            {
                if (block == null || block.GameLogic == null)
                    return;

                var logic = block.GameLogic.GetAs<RecolorableThrustFlameLogic>();

                if (logic != null)
                    sb.Append(logic.m_renderMode.ToString());
            };

            renderMode.SupportsMultipleBlocks = true;

            m_customControls.Add(renderMode);

            var hideFlamesCheckbox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyThrust>("HideThrustFlames");

            hideFlamesCheckbox.Title = MyStringId.GetOrCompute("Hide Thrust Flames");
            hideFlamesCheckbox.Getter = (block) =>
            {
                if (block == null || block.GameLogic == null)
                    return false;

                var logic = block.GameLogic.GetAs<RecolorableThrustFlameLogic>();

                return logic != null ? logic.m_hideFlames : false;
            };

            hideFlamesCheckbox.Setter = (block, value) =>
            {
                var logic = block.GameLogic.GetAs<RecolorableThrustFlameLogic>();

                if (logic != null)
                {
                    logic.m_hideFlames = value;

                    logic.UpdateFlames();
                    logic.UpdateCustomData();
                }
            };

            hideFlamesCheckbox.SupportsMultipleBlocks = true;

            m_customControls.Add(hideFlamesCheckbox);

            var propertyHF = MyAPIGateway.TerminalControls.CreateProperty<bool, IMyThrust>("HideThrustFlames");
                  
            propertyHF.SupportsMultipleBlocks = false;
            propertyHF.Getter = (block) =>
            {
                if (block == null || block.GameLogic == null)
                    return false;

                var logic = block.GameLogic.GetAs<RecolorableThrustFlameLogic>();
                return logic != null ? logic.HideThrustFlames : false;
            };

            propertyHF.Setter = (block, value) =>
            {
                if (block == null || block.GameLogic == null)
                    return;

                var logic = block.GameLogic.GetAs<RecolorableThrustFlameLogic>();
                
                if (logic != null)
                {
                    logic.HideThrustFlames = value;
                }
            };

            MyAPIGateway.TerminalControls.AddControl<IMyThrust>(propertyHF);

            var resetButton = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyThrust>("ResetDefaultColors");

            resetButton.Title = MyStringId.GetOrCompute("Reset Default Colors");

            resetButton.Action = (block) =>
            {
                if (block == null || block.GameLogic == null)
                    return;

                var logic = block.GameLogic.GetAs<RecolorableThrustFlameLogic>();
                
                if (logic != null)
                {
                    var blockDefinition = block.SlimBlock.BlockDefinition as MyThrustDefinition;

                    logic.m_flameIdleColor = blockDefinition.FlameIdleColor;
                    logic.m_flameFullColor = blockDefinition.FlameFullColor;

                    if (logic.m_flameFullColor != logic.m_flameIdleColor)                    
                        logic.m_renderMode = RenderMode.Blended;

                    foreach (var control in m_customControls)
                    {
                        control.UpdateVisual();
                    }

                    logic.UpdateFlames();
                    logic.UpdateCustomData();
                }
            };
            
            resetButton.SupportsMultipleBlocks = true;
            
            m_customControls.Add(resetButton);
        }

        private void CheckFlameDummies()
        {
            if (Entity == null)
                return;

            IMyModel model = Entity.Model;

            if (model == null)
                return;

            Dictionary<string, IMyModelDummy> dummies = new Dictionary<string, IMyModelDummy>();
            model.GetDummies(dummies);

            foreach (string dummy in dummies.Keys)
		    {
			    if (dummy.StartsWith("thruster_flame", StringComparison.InvariantCultureIgnoreCase))
			    {
                    m_hasFlames = true;
                    return;
			    }
		    }
            
            m_hasFlames = false;
        }

        private void ParseCustomData(ref Dictionary<string, List<string>> settings)
        {
            if (m_thruster == null)
                return;

            settings.Clear();

            if (m_thruster.CustomData.Length > 0)
            {
                string[] lines = m_thruster.CustomData.Split('\n');
                string sectionName = null;
                List<string> sectionData = null;
                
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    
                    if (line.StartsWith("["))
                    {
                        if (sectionData != null)
                            settings.Add(sectionName, sectionData);
                        
                        sectionName = line.Substring(1, line.Length - 2);
                        sectionData = new List<string>();

                        continue;
                    }

                    if (line.Length == 0)
                        continue;

                    if (sectionData != null)
                        sectionData.Add(line);
                }

                if (sectionData != null)
                    settings.Add(sectionName, sectionData);
            }
        }

        private Vector4 ParseVector(string s, Vector4 defaultValue)
        {
            var fields = s.Split('/');
            
            try
            {
                return new Vector4(float.Parse(fields[0]), float.Parse(fields[1]), float.Parse(fields[2]), 0.75f);
            }
            catch (System.Exception)
            {
                return defaultValue;
            }
        }

        private void SerializeVector(Vector4 v, StringBuilder sb)
        {
            sb.Append(v.X.ToString("N"));
            sb.Append("/");
            sb.Append(v.Y.ToString("N"));
            sb.Append("/");
            sb.Append(v.Z.ToString("N"));
            sb.Append("\n");
        }

        private void UpdateCustomData()
        {
            if (m_thruster == null)
                return;

            StringBuilder sb = new StringBuilder();
            Dictionary<string, List<string>> settings = new Dictionary<string, List<string>>();

            ParseCustomData(ref settings);
            
            settings.Remove(_CUSTOM_DATA_SECTION);

            foreach (var key in settings.Keys)
            {
                List<string> lines = settings[key];

                sb.Append($"[{key}]\n");

                foreach (var line in lines)
                    sb.Append($"{line}\n");
            }

            sb.Append($"[{_CUSTOM_DATA_SECTION}]\n");
            SerializeVector(FlameIdleColor, sb);
            SerializeVector(FlameFullColor, sb);
            sb.Append($"{m_flameColorsLocked}\n");
            sb.Append($"{m_renderMode.ToString()}\n");
            sb.Append($"{m_hideFlames}\n");

            m_thruster.CustomDataChanged -= OnCustomDataChanged;
            m_thruster.CustomData = sb.ToString();
            m_thruster.CustomDataChanged += OnCustomDataChanged;
        }

        private void UpdateFlames()
        {
            if (Entity == null)
                return;

            var thrust = m_thruster as MyThrust;
            if (thrust == null || thrust.CubeGrid.Physics == null)
                return;

            uint renderObjectID = Entity.Render.GetRenderObjectID();
            if (renderObjectID == 4294967295u)
                return;

            MyThrustDefinition blockDefinition = thrust.BlockDefinition;

            Vector4 flameIdleColor = blockDefinition.FlameIdleColor;
            Vector4 flameFullColor = blockDefinition.FlameFullColor;

            if (m_hideFlames)
            {
                blockDefinition.FlameIdleColor = Vector4D.Zero;
                blockDefinition.FlameFullColor = Vector4D.Zero;
            }
            else
            {
                if (m_renderMode == RenderMode.Separate)
                {
                    var color = thrust.CurrentStrength > 0.001f ? m_flameFullColor : m_flameIdleColor;
                    blockDefinition.FlameIdleColor = color;
                    blockDefinition.FlameFullColor = color;
                }
                else
                {
                    blockDefinition.FlameIdleColor = m_flameIdleColor;
                    blockDefinition.FlameFullColor = m_flameFullColor;
                }
            }

            ((MyRenderComponentThrust)thrust.Render).UpdateFlameAnimatorData();

            blockDefinition.FlameIdleColor = flameIdleColor;
            blockDefinition.FlameFullColor = flameFullColor;
        }

        private static void CustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            if (block is IMyThrust)
            {
                // We don't want to show controls if the thruster doesn't have flames (e.g. hover engines)

                if (block.GameLogic != null && block.GameLogic is RecolorableThrustFlameLogic)
                {
                    var logic = block.GameLogic as RecolorableThrustFlameLogic;
                    if (!logic.HasFlames)
                        return;
                }

                foreach (var item in m_customControls)
                {
                    controls.Add(item);
                }
            }
        }
    }
}