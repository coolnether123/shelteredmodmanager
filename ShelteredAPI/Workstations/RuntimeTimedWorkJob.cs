using System;
using UnityEngine;

namespace ShelteredAPI.Workstations
{
    internal sealed class RuntimeTimedWorkJob : Job
    {
        private readonly float _durationSeconds;
        private readonly string _animationTrigger;
        private readonly string _completeAnimationTrigger;
        private readonly Action _onComplete;
        private readonly Action _onCancelled;
        private bool _callbackInvoked;
        private float _startTime;
        private float _endTime;

        public RuntimeTimedWorkJob(
            string jobType,
            Obj_Base targetObject,
            FamilyMember worker,
            float durationSeconds,
            string animationTrigger,
            string completeAnimationTrigger,
            Action onComplete,
            Action onCancelled)
            : base(
                string.IsNullOrEmpty(jobType) ? "shelteredapi_timed_work" : jobType,
                ResolveInteractionPosition(targetObject),
                worker,
                targetObject)
        {
            _durationSeconds = Mathf.Max(0.1f, durationSeconds);
            _animationTrigger = animationTrigger;
            _completeAnimationTrigger = completeAnimationTrigger;
            _onComplete = onComplete;
            _onCancelled = onCancelled;
        }

        public override string GetJobType()
        {
            return "ShelteredAPI_RuntimeTimedWork";
        }

        public override void Activate()
        {
            if (character == null || obj == null)
            {
                Finish(false);
                return;
            }

            character.ClearPath();
            character.WalkToPosition(location);
            state = Job.JobState.InTransit;
        }

        public override bool BeginJob()
        {
            if (character == null || obj == null)
            {
                Finish(false);
                return false;
            }

            _startTime = Time.time;
            _endTime = _startTime + _durationSeconds;
            state = Job.JobState.Started;

            if (!string.IsNullOrEmpty(_animationTrigger))
                character.TriggerAnim(_animationTrigger);

            SetProgress(0f);
            return true;
        }

        public override void UpdateJob()
        {
            if (GetCancelState() != JobCancelState.Active)
            {
                Finish(false);
                return;
            }

            float progress = Mathf.Clamp((Time.time - _startTime) / Mathf.Max(0.1f, _endTime - _startTime), 0f, 1f);
            SetProgress(progress);
            if (Time.time < _endTime)
                return;

            Finish(true);
        }

        public override void Cancel(bool forced)
        {
            bool wasActive = GetCancelState() == JobCancelState.Active;
            base.Cancel(forced);
            if (wasActive)
                Finish(false);
        }

        private void Finish(bool completed)
        {
            if (_callbackInvoked)
                return;

            _callbackInvoked = true;
            SetProgress(0f);
            if (character != null && !string.IsNullOrEmpty(_completeAnimationTrigger))
                character.TriggerAnim(_completeAnimationTrigger);

            state = Job.JobState.Finished;
            try
            {
                if (completed)
                {
                    if (_onComplete != null)
                        _onComplete();
                }
                else if (_onCancelled != null)
                {
                    _onCancelled();
                }
            }
            finally
            {
                if (character != null)
                    OnFinishedJob();
            }
        }

        private void SetProgress(float progress)
        {
            if (character == null || InteractionManager.Instance == null)
                return;

            InteractionManager.Instance.SetInteractionProgress(character, progress);
        }

        private static Vector3 ResolveInteractionPosition(Obj_Base targetObject)
        {
            if (targetObject == null)
                return Vector3.zero;

            try
            {
                return targetObject.GetInteractionPosition();
            }
            catch
            {
                return targetObject.transform != null ? targetObject.transform.position : Vector3.zero;
            }
        }
    }
}
