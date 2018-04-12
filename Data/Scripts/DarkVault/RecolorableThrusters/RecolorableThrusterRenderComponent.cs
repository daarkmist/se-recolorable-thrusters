using System;
using Sandbox.Definitions;
using Sandbox.Game.Components;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Models;
using VRage.Utils;
using VRageMath;

namespace DarkVault.ThrusterExtensions
{

    public class RecolorableThrusterRenderComponent : MyRenderComponentCubeBlock
    {
        private MyThrust m_thrust;
        private RecolorableThrustFlameLogic m_gameLogic;

        private Vector3 m_originalColorMaskHsv;

        public RecolorableThrusterRenderComponent(MyRenderComponentBase oldRenderer)
        {
            m_originalColorMaskHsv = oldRenderer.ColorMaskHsv;
        }

        public override void OnAddedToContainer()
        {
            base.OnAddedToContainer();
            
            m_thrust = (base.Container.Entity as MyThrust);
            m_gameLogic = m_thrust.GameLogic.GetAs<RecolorableThrustFlameLogic>();

            ColorMaskHsv = m_originalColorMaskHsv;
        }

        public override void OnBeforeRemovedFromContainer()
        {
            base.OnBeforeRemovedFromContainer();
        }

        public override void Draw()
        {
            base.Draw();
            
            if (m_thrust.Flames.Count > 0 && m_thrust.CubeGrid.Physics != null)
            {
                if (m_thrust.IsWorking)
                {
                    UpdateFlameAndColor();

                    Matrix m = default(Matrix);

                    ((MyCubeBlock)m_thrust).GetLocalMatrix(out m);

                    Vector3 translation = m.Translation;
                    uint renderObjectID = m_thrust.CubeGrid.Render.GetRenderObjectID();

                    MyCubeBlock myCubeBlock = base.Container.Entity as MyCubeBlock;
                    float num = (myCubeBlock != null) ? myCubeBlock.CubeGrid.GridScale : 1f;

                    foreach (var flame in m_thrust.Flames)
                    {
                        var flameDirection = Vector3D.TransformNormal(flame.Direction, (MatrixD)m);
                        var flamePosition = Vector3D.TransformNormal(flame.Position, (MatrixD)m) + translation;

                        float radius = m_thrust.ThrustRadiusRand * flame.Radius * num;
                        float length = m_thrust.ThrustLengthRand * flame.Radius * num;
                        float thickness = m_thrust.ThrustThicknessRand * flame.Radius * num;

                        float scale = Math.Max(m_thrust.ThrustLengthRand * MyThrust.THRUST_LENGTH_INTENSITY, 1f);

                        var color = m_thrust.ThrustColor * MyThrust.THRUST_INTENSITY * scale * new Vector4(1f, 1f, 1f, 0.2f);
                        var origin = flamePosition - flameDirection * (double)length * 0.25;

                        if (m_thrust.CurrentStrength > 0f && length > 0f)
                        {
                            MyTransparentGeometry.AddLocalLineBillboard(
                                m_thrust.FlameLengthMaterial, color, origin, renderObjectID, flameDirection, length,
                                MyThrust.THRUST_THICKNESS * thickness);
                        }

                        if (radius > 0f)
                        {
                            MyTransparentGeometry.AddLocalPointBillboard(
                                m_thrust.FlamePointMaterial, color, flamePosition, renderObjectID,
                                MyThrust.THRUST_THICKNESS * radius, 0f);
                        }
                    }
                }

                if (m_thrust.Light != null)
                {
                    m_thrust.UpdateLight();
                }
            }
        }

        private void UpdateFlameAndColor()
        {
            MyThrustDefinition blockDefinition = m_thrust.BlockDefinition;

            Vector4 flameIdleColor = blockDefinition.FlameIdleColor;
            Vector4 flameFullColor = blockDefinition.FlameFullColor;

            blockDefinition.FlameIdleColor = m_gameLogic.FlameIdleColor;
            blockDefinition.FlameFullColor = m_gameLogic.FlameFullColor;

            m_thrust.UpdateThrustFlame();
            m_thrust.UpdateThrustColor();

            blockDefinition.FlameIdleColor = flameIdleColor;
            blockDefinition.FlameFullColor = flameFullColor;
        }
    }
}