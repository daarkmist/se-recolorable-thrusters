using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game;
using VRage.Game.Components;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace DarkVault.ThrusterExtensions
{

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Thrust), false)]
    public class RecolorableThrustFlameLogic : MyGameLogicComponent
    {

        public Vector4 FlameIdleColor
        {
            get { return m_flameIdleColor; }
            set
            {
                if (!m_flameColorsLocked)
                    m_flameIdleColor = value;

                    UpdateCustomData();
            }
        }

        public Vector4 FlameFullColor
        {
            get { return m_flameFullColor; }
            set
            {
                if (!m_flameColorsLocked)
                    m_flameFullColor = value;

                    UpdateCustomData();
            }
        }

        private static List<IMyTerminalControl> m_customControls = new List<IMyTerminalControl>();

        private Vector4 m_flameIdleColor;
        private Vector4 m_flameFullColor;
        private bool m_flameColorsLocked;
        private IMyThrust m_thruster;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            m_thruster = Entity as IMyThrust;

            var oldRenderer = m_thruster.Render;

            m_thruster.Components.Remove(typeof(MyRenderComponentBase));
            m_thruster.Components.Add(typeof(MyRenderComponentBase), new RecolorableThrusterRenderComponent(oldRenderer));

            InitFlameColors();

            lock (m_customControls)
            {
                if (m_customControls.Count == 0)
                    CreateTerminalControls();
            }
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
                var logic = block.GameLogic.GetAs<RecolorableThrustFlameLogic>();

                return logic != null ? logic.m_flameColorsLocked : false;
            };

            checkbox.Setter = (block, value) =>
            {
                var logic = block.GameLogic.GetAs<RecolorableThrustFlameLogic>();

                if (logic != null)
                    logic.m_flameColorsLocked = value;
            };

            checkbox.SupportsMultipleBlocks = true;

            m_customControls.Add(checkbox);

            // FlameIdleColor

            IMyTerminalControlColor color = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlColor, IMyThrust>("FlameIdleColor");

            color.Title = MyStringId.GetOrCompute("Idle");

            color.Getter = (block) =>
            {
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

                    logic.UpdateCustomData();
                }
            };

            m_customControls.Add(color);

            var propertyIC = MyAPIGateway.TerminalControls.CreateProperty<Vector4, IMyThrust>("FlameIdleColorOverride");
                  
            propertyIC.SupportsMultipleBlocks = false;
            propertyIC.Getter = (block) =>
            {
                var logic = block.GameLogic.GetAs<RecolorableThrustFlameLogic>();
                return logic != null ? logic.FlameIdleColor : Vector4.Zero;
            };

            propertyIC.Setter = (block, value) =>
            {
                if (block == null)
                    return;

                if (block.GameLogic == null)
                    return;

                var logic = block.GameLogic.GetAs<RecolorableThrustFlameLogic>();

                if (logic != null)
                {
                    logic.FlameIdleColor = new Vector4(value.X, value.Y, value.Z, 0.75f);
                }
            };

            MyAPIGateway.TerminalControls.AddControl<IMyThrust>(propertyIC);

            // FlameFullColor

            color = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlColor, IMyThrust>("FlameFullColor");

            color.Title = MyStringId.GetOrCompute("Full");

            color.Getter = (block) =>
            {
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

                    logic.UpdateCustomData();
                }
            };

            m_customControls.Add(color);

            var propertyFC = MyAPIGateway.TerminalControls.CreateProperty<Vector4, IMyThrust>("FlameFullColorOverride");
                  
            propertyFC.SupportsMultipleBlocks = false;
            propertyFC.Getter = (block) =>
            {
                var logic = block.GameLogic.GetAs<RecolorableThrustFlameLogic>();
                return logic != null ? logic.FlameFullColor : Vector4.Zero;
            };

            propertyFC.Setter = (block, value) =>
            {
                if (block == null)
                    return;

                if (block.GameLogic == null)
                    return;

                var logic = block.GameLogic.GetAs<RecolorableThrustFlameLogic>();
                
                if (logic != null)
                {
                    logic.FlameFullColor = new Vector4(value.X, value.Y, value.Z, 0.75f);
                }
            };

            MyAPIGateway.TerminalControls.AddControl<IMyThrust>(propertyFC);

            var resetButton = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyThrust>("ResetDefaultColors");

            resetButton.Title = MyStringId.GetOrCompute("Reset Default Colors");

            resetButton.Action = (block) =>
            {
                var logic = block.GameLogic.GetAs<RecolorableThrustFlameLogic>();
                
                if (logic != null)
                {
                    var blockDefinition = m_thruster.SlimBlock.BlockDefinition as MyThrustDefinition;

                    m_flameIdleColor = blockDefinition.FlameIdleColor;
                    m_flameFullColor = blockDefinition.FlameFullColor;

                    foreach (var control in m_customControls)
                    {
                        if (control is IMyTerminalControlColor)
                            control.UpdateVisual();
                    }

                    logic.UpdateCustomData();
                }
            };
            
            resetButton.SupportsMultipleBlocks = true;
            
            m_customControls.Add(resetButton);
        }

        private void InitFlameColors()
        {
            var blockDefinition = m_thruster.SlimBlock.BlockDefinition as MyThrustDefinition;

            m_flameIdleColor = blockDefinition.FlameIdleColor;
            m_flameFullColor = blockDefinition.FlameFullColor;

            string[] lines = m_thruster.CustomData.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] == "[FlameColors]")
                {
                    if (i + 1 < lines.Length)
                        m_flameIdleColor = ParseVector(lines[i + 1], m_flameIdleColor);

                    if (i + 2 < lines.Length)
                        m_flameFullColor = ParseVector(lines[i + 2], m_flameFullColor);

                    if (i + 3 < lines.Length && lines[i + 3].Length > 0)
                        m_flameColorsLocked = bool.Parse(lines[i + 3]);

                    break;
                }
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
        }

        private void UpdateCustomData()
        {
            if (m_thruster == null)
                return;

            StringBuilder sb = new StringBuilder();

            if (m_thruster.CustomData.Length > 0)
            {
                string[] lines = m_thruster.CustomData.Split('\n');

                int i = 0;
                while (i < lines.Length)
                {
                    if (lines[i] == "[FlameColors]")
                    {
                        i += 3;

                        if (lines[i] == "True" || lines[i] == "False")
                            i++;

                        continue;
                    }

                    if (lines[i].Length > 0)
                    {
                        sb.Append(lines[i]);
                        sb.Append("\n");
                    }

                    i++;
                }
            }

            sb.Append("[FlameColors]\n");
            SerializeVector(FlameIdleColor, sb);
            sb.Append("\n");
            SerializeVector(FlameFullColor, sb);
            sb.Append("\n");
            sb.Append(m_flameColorsLocked);
            sb.Append("\n");

            m_thruster.CustomData = sb.ToString();
        }

        private static void CustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            if (block is IMyThrust)
            {
                foreach (var item in m_customControls)
                {
                    controls.Add(item);
                }
            }
        }
    }
}