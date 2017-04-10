﻿using UnityEngine;
using System.Collections.Generic;
using Client.Common;
using Client.Data;
using Utility;
using Utility.Export;
using Effect;
using Client.Effect;
using Game;
/*----------------------------------------------------------------
// 模块名：MainStage
// 创建者：chen
// 修改者列表：
// 创建日期：2017.3.18
// 模块描述：顺序播放主阶段
//--------------------------------------------------------------*/
/// <summary>
/// 顺序播放主阶段
/// </summary>
public class MainStage : SeqBuilder
{
    public enumSequenceType showType = enumSequenceType.e_Sequence_Skill;
    public long AttackerId;//攻击者ID
    public int SkillId;//技能ID
    public List<long> BeAttackerList = new List<long>();//被攻击者的ID
    public List<CVector3> BeAttackPosList = new List<CVector3>();//攻击目标位置
    public Dictionary<long, List<KeyValuePair<int, int>>> HpChangeInfo = new Dictionary<long, List<KeyValuePair<int, int>>>(); //血量改变的信息
    public HashSet<long> MissInfo = new HashSet<long>();//攻击MIss的信息
    public Dictionary<long, long> DeadList = new Dictionary<long, long>();//死亡列表,key=BeAttackId,value=AttackerId
    public long firstBloodBeastId = 0;//第一滴血的神兽ID；
    public float LastDeadTime = -1;//最近死亡的时间
    private CameraMoveRecord record;//摄像机移动记录
    public int CameraAnimEft = 0;//摄像机的特效表现标示量，为0,1分别代表不同的特效
    public List<PosChange> AttackerPosChanges = new List<PosChange>();//攻击者的位置改变
    public Dictionary<long, List<PosChange>> BeAttackerPosChanges = new Dictionary<long, List<PosChange>>();//被攻击者的位置改变
    public override void BuildSeq()
    {
        float time2 = 0f;
        switch (this.showType)
        {
            case enumSequenceType.e_Sequence_Skill:
                time2 = this.BuildSkillAction();
                break;
        }
        this.EndTime = time2;
    }

    public float BuildSkillAction()
    {
        float allTime = this.LastAnimEndTime;
        float fLastAnimTime = this.LastAnimEndTime;
        Beast attackBeast = Singleton<BeastManager>.singleton.GetBeastById(this.AttackerId);
        DataSkillShow data;
        if (attackBeast != null)
        {
            data = attackBeast.GetSkillShow(this.SkillId);
        }
        else
        {
            data = new DataSkillShow();
        }
        //allTime = this.BuildScreenBlurShow(allTime, data, true);
        //float fCameraAnimEndTime = 0f;
        //allTime = this.BuildCameraAnimShow(allTime, ref fCameraAnimEndTime, data);
        this.BuildAttackSkillAnimShow(allTime, ref fLastAnimTime, data);
        float fSkillEffectTime = this.BuildAttackSkillEffectShow(allTime);
        this.BuildAttackPosChangeShow(allTime, ref fLastAnimTime, data);
        bool flag = true;
        bool beAttackEffect = data != null && data.BeAttackEffectHitPoint;
        foreach (long current in this.BeAttackerList)
        {
            float fBeAttackAnimStartTime = 0;
            float beAttackTime = this.BuildBeAttackSkillAnimShow(current, allTime, flag, data, ref fBeAttackAnimStartTime);
            if (beAttackEffect)
            {
                fSkillEffectTime = fBeAttackAnimStartTime;
            }
            this.BuildBeAttackSkillEffectShow(fSkillEffectTime, true, current);
            float beAttackPosChangeTime = this.BuildBeAttackPosChangeShow(current, allTime, data, ref fLastAnimTime);
            float time = Mathf.Max(beAttackTime, beAttackPosChangeTime);
            time = this.BuildBeAttackDeadShow(current, time);
            flag = false;
            allTime = Mathf.Max(allTime, time);
        }
        return allTime;
    }
    #region 私有方法
    /// <summary>
    /// 攻击者技能攻击动画的表现，返回攻击动作的时间
    /// </summary>
    /// <param name="fStartTime"></param>
    /// <param name="fAnimEndTime"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    private float BuildAttackSkillAnimShow(float fStartTime,ref float fAnimEndTime,DataSkillShow data)
    {
        float time = fStartTime;
        if (data != null)
        {
            float delayTime = data.AttackAnimStartDelayTime > 0 ? data.AttackAnimStartDelayTime : 0f;
            AttackSkillTrigger trigger = new AttackSkillTrigger();
            trigger.SkillId = this.SkillId;
            trigger.AttackerId = this.AttackerId;
            trigger.BeAttackerId = this.BeAttackerList;
            trigger.BeAttackerPos = this.BeAttackPosList;
            trigger.StartTime = time + delayTime;
            trigger.Duration = trigger.GetDuration();
            base.AddEvent(trigger);
            time = trigger.StartTime + trigger.Duration;
            fAnimEndTime = time;
        }
        return time;
    }
    /// <summary>
    /// 创建攻击者位置改变表现
    /// </summary>
    /// <param name="fStartTime"></param>
    /// <param name="fAnimEndTime"></param>
    /// <param name="dataSkillShow"></param>
    /// <returns></returns>
    private float BuildAttackPosChangeShow(float fStartTime, ref float fAnimEndTime, DataSkillShow dataSkillShow)
    {
        float time = fStartTime;
        float delayTime = dataSkillShow == null ? 0f : dataSkillShow.AttackJumpStartDelayTime;
        foreach (var current in this.AttackerPosChanges)
        {
            ChangePosTrigger trigger = new ChangePosTrigger();
            trigger.controlData = current;
            trigger.StartTime = time + delayTime;
            if (dataSkillShow != null)
            {
                trigger.JumpEndAnim = dataSkillShow.AttackJumpEndAnim;
                trigger.JumpDuraAnim = dataSkillShow.AttackJumpDuraAnim;
                if (current.type == ChangePosType.e_Jump)
                {
                    trigger.Jumptime = ((dataSkillShow.AttackJumpSpeed <= 0f) ? dataSkillShow.AttackJumpTime : (this.GetDistanceByPos(current.DestPos[0]) / dataSkillShow.AttackJumpSpeed));
                    trigger.EffectId = dataSkillShow.AttackJumpEffect;
                    trigger.Height = dataSkillShow.AttackJumpHeight;
                }
                else if (current.type == ChangePosType.e_Walk)
                {
                    trigger.EffectId = dataSkillShow.AttackJumpEffect;
                }
            }
            trigger.Duration = trigger.GetDuration();
            trigger.SkillId = this.SkillId;
            trigger.IsForward = SkillGameManager.IsAttackForward(this.SkillId);
            if (!current.IgnoreDuration)
            {
                time = trigger.StartTime + trigger.Duration + 0.1f;
                fAnimEndTime = time;
            }
            if (this.BeAttackerList.Count > 0 && this.AttackerId != this.BeAttackerList[0])
            {
                trigger.TargetPlayerID = this.BeAttackerList[0];
            }
            base.AddEvent(trigger);
        }
        return time;
    }
    /// <summary>
    /// 创建被攻击者位置改变表现
    /// </summary>
    /// <param name="beAttackId"></param>
    /// <param name="fStartTime"></param>
    /// <param name="data"></param>
    /// <param name="fAnimEndTime"></param>
    /// <returns></returns>
    private float BuildBeAttackPosChangeShow(long beAttackId, float fStartTime, DataSkillShow data, ref float fAnimEndTime)
    {
        float allTime = fStartTime;
        float delayTime = data == null ? 0f : data.BeAttackJumpStartDelayTime;
        Vector3 vec3DestPos = (this.BeAttackPosList.Count > 0) ? Hexagon.GetHex3DPos(this.BeAttackPosList[0], Space.World) : Vector3.zero;
        float hitTime = SkillGameManager.GetSkillHitTime(this.SkillId, this.AttackerId, beAttackId, vec3DestPos,EffectInstanceType.Trace);
        if (this.BeAttackerPosChanges.ContainsKey(beAttackId))
        {
            foreach (var current in this.BeAttackerPosChanges[beAttackId])
            {
                ChangePosTrigger trigger = new ChangePosTrigger();
                trigger.SkillId = this.SkillId;
                trigger.TargetPlayerID = this.AttackerId;
                trigger.controlData.type = current.type;
                trigger.controlData.PlayerId = current.PlayerId;
                trigger.controlData.DestPos.AddRange(current.DestPos);
                trigger.IsForward = SkillGameManager.IsBeAttackForward(this.SkillId);
                if (null != data)
                {
                    trigger.JumpEndAnim = data.BeAttackJumpEndAnim;
                    trigger.JumpDuraAnim = data.BeAttackJumpDuraAnim;
                    if (current.type == ChangePosType.e_Jump)
                    {
                        trigger.Jumptime = ((data.BeAttackJumpSpeed <= 0f) ? data.BeAttackJumpTime : (this.GetDistanceByPos(current.DestPos[0]) / data.BeAttackJumpSpeed));
                        trigger.Height = data.BeAttackJumpHeight;
                        trigger.EffectId = data.BeAttackJumpEffect;
                    }
                    else if (current.type == ChangePosType.e_Walk)
                    {
                        trigger.EffectId = data.BeAttackJumpEffect;
                    }
                }
                trigger.StartTime = allTime + delayTime + hitTime;
                trigger.Duration = trigger.GetDuration();
                base.AddEvent(trigger);
                allTime = trigger.StartTime + trigger.Duration;
                fAnimEndTime = allTime;
            }
        }
        return allTime;
    }
    private float BuildAttackSkillEffectShow(float fStartTime)
    {
        float time = fStartTime;
        if (!GameConfig.singleton.NoAttackSkills.Contains(this.SkillId))
        {
            AttackSkillEffectTrigger attackSkillEffectTrigger = new AttackSkillEffectTrigger();
            attackSkillEffectTrigger.SkillID = this.SkillId;
            attackSkillEffectTrigger.AttackerId = this.AttackerId;
            attackSkillEffectTrigger.ListBeAttackerId = this.BeAttackerList;
            attackSkillEffectTrigger.ListBeAttackPos = this.BeAttackPosList;
            attackSkillEffectTrigger.StartTime = time;
            attackSkillEffectTrigger.Duration = attackSkillEffectTrigger.GetHitTime();
            base.AddEvent(attackSkillEffectTrigger);
            time = attackSkillEffectTrigger.StartTime + attackSkillEffectTrigger.Duration;
        }
        return time;
    }
    private float BuildBeAttackSkillAnimShow(long beAttacker, float fStartTime, bool bMainBeAttack, DataSkillShow data, ref float fBeAttackStartTime)
    {
        float allTime = fStartTime;
        if (data != null)
        {
            Vector3 pos = this.BeAttackPosList.Count > 0 ? Hexagon.GetHex3DPos(this.BeAttackPosList[0], Space.World) : Vector3.zero;
            float hitTime = 0;
            //如果是普通攻击的话
            if (this.SkillId == 1)
            {
                hitTime = SkillGameManager.GetSkillHitTime(this.SkillId, this.AttackerId, beAttacker, pos);
            }
            else
            {
                //如果是技能的话
                hitTime = SkillGameManager.GetSkillHitTime(this.SkillId, this.AttackerId, beAttacker, pos, EffectInstanceType.Trace);
                if (data.AttackJumpSpeed > 0)
                {
                    hitTime += this.GetDistanceByTargetBeastId(beAttacker) / data.AttackJumpSpeed;
                }
            }
            float beAttackAnimStartDelayTime = data.AttackAnimStartDelayTime;
            allTime += beAttackAnimStartDelayTime + hitTime;
            fBeAttackStartTime = allTime;
            if (this.HpChangeInfo.ContainsKey(beAttacker))
            {
                bool ShowAnim = false;
                if (!this.MissInfo.Contains(beAttacker))
                {
                    for (int i = 0; i < this.HpChangeInfo[beAttacker].Count; i++)
                    {
                        KeyValuePair<int, int> keyValuePair = this.HpChangeInfo[beAttacker][i];
                        if (keyValuePair.Key < keyValuePair.Value)
                        {
                            ShowAnim = true;
                        }
                    }
                }
                //配置文件里面配置被攻击者是否被攻击次数多次，如果不是就是默认1次
                int attackCount = data.BeAttackCount > 0 ? data.BeAttackCount : 1;
                string[] arrayTime = string.IsNullOrEmpty(data.BeAttackSpaceTime) ? new string[0] : data.BeAttackSpaceTime.Split(';');
                for (int i=0; i < attackCount;i++)
                {
                    BeAttackSkillTrigger trigger = new BeAttackSkillTrigger();
                    trigger.AttackerId = this.AttackerId;
                    trigger.BeAttackerId = beAttacker;
                    trigger.StartTime = allTime;
                    if (arrayTime.Length > 0)
                    {
                        trigger.IsSpaceAnim = i < attackCount - 1;
                        float duration = 0;
                        if (i < arrayTime.Length)
                        {
                            float.TryParse(arrayTime[i], out duration);
                        }
                        trigger.Duration = duration;
                    }
                    else if (data.BeAttackDuraTime > 0)
                    {
                        trigger.IsDuraAnim = true;
                        trigger.Duration = data.BeAttackDuraTime;
                    }
                    else
                    {
                        trigger.Duration = trigger.GetDuration();

                    }
                    trigger.MainBeAttcker = beAttacker;
                    trigger.ShowAnim = ShowAnim;
                    if (this.HpChangeInfo[beAttacker].Count > 0)
                    {
                        trigger.HpChange = this.HpChangeInfo[beAttacker][0].Key - this.HpChangeInfo[beAttacker][0].Value;
                    }
                    base.AddEvent(trigger);
                    if (i == attackCount - 1)
                    {
                        float fStartTime1 = data.BeAttackDuraTime > 0 ? trigger.StartTime + trigger.Duration : allTime;
                        this.BuildHpChangeShow(fStartTime1, beAttacker);
                    }
                    allTime = trigger.StartTime + trigger.Duration;
                }
            }
        }
        return allTime;
    }
    /// <summary>
    /// 建立扣血表现事件
    /// </summary>
    private float BuildHpChangeShow(float fStartTime,long beAttacker)
    {
        float time = fStartTime;
        if (this.HpChangeInfo.ContainsKey(beAttacker))
        {
            if (!this.MissInfo.Contains(beAttacker))
            {
                for (int i = 0; i < this.HpChangeInfo[beAttacker].Count; i++)
                {
                    KeyValuePair<int, int> hpKeyValue = this.HpChangeInfo[beAttacker][i];
                    HpChangeTrigger trigger = new HpChangeTrigger();
                    trigger.AttackId = this.AttackerId;
                    trigger.BeAttackId = beAttacker;
                    trigger.StartTime = fStartTime;
                    trigger.HpValue = hpKeyValue.Key;
                    trigger.OgrinHpValue = hpKeyValue.Value;
                    trigger.Duration = trigger.GetDuration();
                    base.AddEvent(trigger);
                    time = trigger.StartTime + trigger.Duration;
                }
            }
        }
        return time;
    }
    /// <summary>
    /// 创建被攻击者死亡的表现
    /// </summary>
    /// <param name="beAttackId"></param>
    /// <param name="fStartTime"></param>
    /// <returns></returns>
    private float BuildBeAttackDeadShow(long beAttackId,float fStartTime)
    {
        float time = fStartTime;
        if (this.DeadList.ContainsKey(beAttackId))
        {
            DeadTrigger trigger = new DeadTrigger();
            if (beAttackId == this.firstBloodBeastId)
            {
                trigger.bIsFirstBoold = true;
            }
            trigger.BeAttackId = beAttackId;
            trigger.AttackerId = this.DeadList[beAttackId];
            trigger.StartTime = time;
            if (this.LastDeadTime < trigger.StartTime)
            {
                this.LastDeadTime = trigger.StartTime;
            }
            trigger.Duration = trigger.GetDuration() + 1f;
            base.AddEvent(trigger);
            time = trigger.StartTime + trigger.Duration;
        }
        return time;
    }
    /// <summary>
    /// 创建摄像机震动的特效表现
    /// </summary>
    /// <param name="fStartTime"></param>
    /// <param name="data"></param>
    private float BuildCameraShakeShow(float fStartTime, DataSkillShow data)
    {
        float allTime = fStartTime;
        if (this.CameraAnimEft == 0)
        {
            if (data != null && data.CameraMove == 1)
            {
                CameraBackSmoothTrigger trigger = new CameraBackSmoothTrigger();
                trigger.AttackerId = this.AttackerId;
                trigger.record = this.record;
                trigger.StartTime = fStartTime;
                trigger.Duration = data.CameraMoveDurationTime;
                base.AddEvent(trigger);
                allTime = Mathf.Max(allTime, trigger.StartTime + trigger.Duration);
            }
        }
        return allTime;
    }
    /// <summary>
    /// 创建摄像机移动特效
    /// </summary>
    /// <param name="fStartTime"></param>
    /// <param name="fAnimCtrlEndtime"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    private float BuildCameraAnimShow(float fStartTime,ref float fAnimCtrlEndtime, DataSkillShow data)
    {
        float time = fStartTime;
        if (data != null)
        {
            if (this.CameraAnimEft == 0)
            {
                if (data.CameraMove == 1)
                {
                    this.record = new CameraMoveRecord();
                    CameraGoTrigger cameraGoTrigger = new CameraGoTrigger();
                    cameraGoTrigger.record = this.record;
                    cameraGoTrigger.StartTime = time;
                    cameraGoTrigger.AttackerId = this.AttackerId;
                    cameraGoTrigger.BeAttackIdList = this.BeAttackerList;
                    cameraGoTrigger.Duration = data.CameraMoveDurationTime;
                    base.AddEvent(cameraGoTrigger);
                    time = cameraGoTrigger.StartTime + cameraGoTrigger.Duration;
                }
            }
            else
            {
                CameraAnimTrigger cameraAnimTrigger = new CameraAnimTrigger();
                cameraAnimTrigger.StartTime = time;
                cameraAnimTrigger.EffectId = this.CameraAnimEft;
                cameraAnimTrigger.PlayerId = this.AttackerId;
                cameraAnimTrigger.Duration = cameraAnimTrigger.GetDuration();
                base.AddEvent(cameraAnimTrigger);
                fAnimCtrlEndtime = cameraAnimTrigger.StartTime + EffectManager.Instance.GetEffectCameraControlTime(this.CameraAnimEft);
                time = cameraAnimTrigger.StartTime + EffectManager.Instance.GetEffectCameraControlDelay(this.CameraAnimEft);
            }
        }
        return time;
    }
    /// <summary>
    /// 开始屏幕模糊
    /// </summary>
    /// <param name="fStartTime"></param>
    /// <param name="dataSkillShow"></param>
    /// <param name="bEnter"></param>
    /// <returns></returns>
    private float BuildScreenBlurShow(float fStartTime,DataSkillShow dataSkillShow,bool bEnter)
    {
        float time = fStartTime;
        if (dataSkillShow != null)
        {
            if (dataSkillShow.ScreenBlur == 1)
            {
                ScreenBlurTrigger screenBlurTrigger = new ScreenBlurTrigger();
                screenBlurTrigger.m_vAffectedPlayer.AddRange(this.BeAttackerList);
                if (!screenBlurTrigger.m_vAffectedPlayer.Contains(this.AttackerId))
                {
                    screenBlurTrigger.m_vAffectedPlayer.Add(this.AttackerId);
                }
                screenBlurTrigger.StartTime = time;
                screenBlurTrigger.bindPlayerID = this.AttackerId;
                screenBlurTrigger.endDisable = !bEnter;
                screenBlurTrigger.m_fStartAlpha = (bEnter ? 0f : (dataSkillShow.BlackDepth * 0.5f));
                screenBlurTrigger.m_fEndAlpha = (bEnter ? (dataSkillShow.BlackDepth * 0.5f) : 0f);
                screenBlurTrigger.Duration = (bEnter ? dataSkillShow.ScreenBlurDurationTime1 : dataSkillShow.ScreenBlurDurationTime2);
                base.AddEvent(screenBlurTrigger);
                time = screenBlurTrigger.StartTime + screenBlurTrigger.Duration;
            }
        }
        return time;
    }
    /// <summary>
    /// 创建被攻击者的技能攻击特效（包括Miss漂浮文字和特效表现）
    /// </summary>
    /// <param name="fStartTime"></param>
    /// <param name="bMainBeAttack"></param>
    /// <param name="BeAtttackerId"></param>
    /// <returns></returns>
    private float BuildBeAttackSkillEffectShow(float fStartTime, bool bMainBeAttack,long BeAtttackerId)
    {

        BeAttackSkillEffectTrigger trigger = new BeAttackSkillEffectTrigger();
        trigger.StartTime = fStartTime;
        trigger.MainBeAttacker = bMainBeAttack;
        trigger.AttackerId = this.AttackerId;
        trigger.BeAttackerId = BeAtttackerId;
        trigger.BeAttackPos = this.BeAttackPosList;
        trigger.SkillId = this.SkillId;
        if (this.HpChangeInfo.ContainsKey(BeAtttackerId) && this.MissInfo.Contains(BeAtttackerId))
        {
            trigger.bShowMissEffect = true;
        }
        trigger.Duration = trigger.GetHitTime();
        base.AddEvent(trigger);
        return trigger.StartTime + trigger.Duration;
    }
    /// <summary>
    /// 取得攻击者和被攻击者之间的距离
    /// </summary>
    /// <param name="beAttackerId"></param>
    /// <returns></returns>
    private uint GetDistanceByTargetBeastId(long beAttackerId)
    {
        Beast attacker = Singleton<BeastManager>.singleton.GetBeastById(this.AttackerId);
        Beast beAttacker = Singleton<BeastManager>.singleton.GetBeastById(beAttackerId);
        if (attacker != null && beAttacker != null)
        {
            int nRow = 0;
            int nCol = 0;
            Hexagon.GetHexIndexByPos(attacker.MovingPos, out nRow, out nCol);
            CVector3 pos = new CVector3();
            pos.nRow = nRow;
            pos.nCol = nCol;
            Hexagon.GetHexIndexByPos(beAttacker.MovingPos, out nRow, out nCol);
            CVector3 pos2 = new CVector3();
            pos2.nRow = nRow;
            pos2.nCol = nCol;
            return Singleton<ClientMain>.singleton.scene.CalDistance(pos, pos2);
        }
        else
        {
            return 0u;
        }
    }
    /// <summary>
    /// 攻击者距离目标位置的距离
    /// </summary>
    /// <param name="destPos"></param>
    /// <returns></returns>
    private uint GetDistanceByPos(CVector3 destPos)
    {
        uint result = 0u;
        Beast heroById = Singleton<BeastManager>.singleton.GetBeastById(this.AttackerId);
        if (null != heroById)
        {
            int nRow = 0;
            int nCol = 0;
            Hexagon.GetHexIndexByPos(heroById.MovingPos, out nRow, out nCol);
            CVector3 cVector = new CVector3();
            cVector.nRow = nRow;
            cVector.nCol = nCol;
            result = Singleton<ClientMain>.singleton.scene.CalDistance(cVector, destPos);
        }
        return result;
    }
    #endregion
}
