using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace Klime.ACOGScope
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class Scope : MySessionComponentBase
    {
        private int _timer;
        private IMyCubeGrid scope_grid;
        private MyObjectBuilder_EntityBase scope_OB;
        private MyCameraBlock scope_block;
        private bool in_scope;
        private MatrixD scope_matrix;
        private MatrixD player_matrx;
        private MatrixD prev_stabilize = MatrixD.Zero;
        private MatrixD prev_rifle_mat = MatrixD.Zero;
        private MatrixD prev_char_mat = MatrixD.Zero;
        private IMyAutomaticRifleGun rifle;

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            MyVisualScriptLogicProvider.PrefabSpawnedDetailed += PrefabSpawnedDetailed;
        }

        private void PrefabSpawnedDetailed(long entityid, string prefabname)
        {
            if (prefabname == "ACOGScope")
            {
                var scopeent = MyAPIGateway.Entities.GetEntityById(entityid) as IMyCubeGrid;
                if (scopeent != null)
                {
                    scopeent.Physics.Enabled = false;
                    scope_OB = scopeent.GetObjectBuilder();
                    scopeent.Close();
                }
            }
        }

        private void SpawnScope(MatrixD inc_matrix)
        {
            Vector3D base_pos = inc_matrix.Translation + inc_matrix.Forward + inc_matrix.Up;
            Vector3D empt_pos = Vector3D.Zero;
            MyVisualScriptLogicProvider.FindFreePlace(base_pos, out empt_pos, 100f, 20, 5, 10);
            if (empt_pos != Vector3D.Zero)
            {
                MyVisualScriptLogicProvider.SpawnPrefab("ACOGScope", empt_pos, inc_matrix.Forward, inc_matrix.Up);
            }
            else
            {
                MyVisualScriptLogicProvider.SpawnPrefab("ACOGScope", base_pos, inc_matrix.Forward, inc_matrix.Up);
            }
        }

        private void SpawnCamera()
        {
            MyAPIGateway.Entities.RemapObjectBuilder(scope_OB);
            var ent = MyAPIGateway.Entities.CreateFromObjectBuilder(scope_OB) as MyEntity;
            if (ent != null)
            {
                ent.IsPreview = true;
                ent.SyncFlag = false;
                ent.Save = false;
                MyAPIGateway.Entities.AddEntity(ent);

                scope_grid = ent as IMyCubeGrid;
                if (scope_grid != null && !scope_grid.MarkedForClose)
                {
                    scope_grid.Physics.Enabled = false;
                    scope_grid.Render.RemoveRenderObjects();
                    var cubeGridTemp = scope_grid as MyCubeGrid;
                    cubeGridTemp.IsPreview = true;
                    cubeGridTemp.SyncFlag = false;
                    cubeGridTemp.Save = false;
                    scope_grid.CustomName = "";
                    var allb = new List<IMySlimBlock>();
                    scope_grid.GetBlocks(allb);
                    foreach (var block in allb)
                    {
                        if (block.FatBlock != null && block.FatBlock is IMyCameraBlock)
                        {
                            scope_block = block.FatBlock as MyCameraBlock;
                        }
                    }
                }
            }
        }

        public override void UpdateBeforeSimulation()
        {
            if (MyAPIGateway.Session?.Player?.Character != null)
            {
                if (scope_OB == null)
                {
                    SpawnScope(MyAPIGateway.Session.Player.Character.WorldMatrix);
                }

                if (scope_OB != null && (scope_grid == null || scope_grid.MarkedForClose))
                {
                    SpawnCamera();
                }

                if (scope_grid != null && scope_block != null && !scope_grid.MarkedForClose)
                {
                    if (MyAPIGateway.Session.Player.Character != null)
                    {
                        if (MyAPIGateway.Session.Player.Character.EquippedTool is IMyAutomaticRifleGun)
                        {
                            rifle = MyAPIGateway.Session.Player.Character.EquippedTool as IMyAutomaticRifleGun;
                            if (rifle.DefinitionId.SubtypeName.Contains("(ACOG)"))
                            {
                                if (in_scope)
                                {
                                    if (MyAPIGateway.Input.IsAnyShiftKeyPressed())
                                    {
                                        var mouseX = MyAPIGateway.Input.GetMouseXForGamePlay();
                                        var mouseY = MyAPIGateway.Input.GetMouseYForGamePlay();

                                        if (prev_stabilize == MatrixD.Zero)
                                        {
                                            prev_stabilize = scope_grid.WorldMatrix;
                                            prev_rifle_mat = rifle.WorldMatrix;
                                            prev_char_mat = MyAPIGateway.Session.Player.Character.WorldMatrix;
                                        }
                                        else
                                        {
                                            if (Math.Abs(mouseX) > 0)
                                            {
                                                prev_stabilize.Forward = Vector3D.Rotate(prev_stabilize.Forward, MatrixD.CreateRotationZ(0.001f * -1 * mouseX));
                                            }
                                            if (Math.Abs(mouseY) > 0)
                                            {
                                                prev_stabilize.Forward = Vector3D.Rotate(prev_stabilize.Forward, MatrixD.CreateRotationY(0.001f * -1 * mouseY));
                                            }
                                            scope_grid.WorldMatrix = prev_stabilize;
                                            prev_rifle_mat = prev_stabilize;
                                            prev_rifle_mat.Translation += prev_rifle_mat.Up * 0.3f;
                                            rifle.WorldMatrix = prev_rifle_mat;
                                            MyAPIGateway.Session.Player.Character.WorldMatrix = prev_char_mat;
                                        }
                                    }
                                    else
                                    {
                                        prev_stabilize = MatrixD.Zero;
                                        scope_matrix = rifle.WorldMatrix;
                                        scope_matrix.Translation = rifle.WorldMatrix.Translation + rifle.WorldMatrix.Down * 0.31f;
                                        scope_grid.WorldMatrix = scope_matrix;
                                    }
                                    if (MyAPIGateway.Input.IsLeftMousePressed())
                                    {
                                        MyGunStatusEnum verify;
                                        if (rifle.CanShoot(MyShootActionEnum.PrimaryAction, MyAPIGateway.Session.Player.IdentityId, out verify))
                                        {
                                            if (verify == MyGunStatusEnum.OK)
                                            {
                                                rifle.Shoot(MyShootActionEnum.PrimaryAction, rifle.WorldMatrix.Forward, null);
                                            }
                                        }
                                    }
                                    scope_block.RequestSetView();
                                }
                                if (!MyAPIGateway.Gui.IsCursorVisible && !MyAPIGateway.Gui.ChatEntryVisible && !MyAPIGateway.Input.IsAnyAltKeyPressed() && MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.None)
                                {
                                    if (MyAPIGateway.Input.IsNewRightMousePressed())
                                    {
                                        in_scope = !in_scope;
                                    }
                                }
                            }
                            else
                            {
                                scope_grid.WorldMatrix = MyAPIGateway.Session.Player.Character.WorldMatrix;
                                prev_stabilize = MatrixD.Zero;
                            }
                        }
                        else
                        {
                            scope_grid.WorldMatrix = MyAPIGateway.Session.Player.Character.WorldMatrix;
                            prev_stabilize = MatrixD.Zero;
                        }
                    }
                }
            }
            _timer += 1;
        }

        protected override void UnloadData()
        {
            MyVisualScriptLogicProvider.PrefabSpawnedDetailed -= PrefabSpawnedDetailed;
        }
    }
}