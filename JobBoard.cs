using System;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Job board displayed at station bars with available missions
    /// </summary>
    public class JobBoard
    {
        private List<Mission> _availableMissions = new();
        private MissionManager _missionManager;

        public IReadOnlyList<Mission> AvailableMissions => _availableMissions.AsReadOnly();
        public int SelectedIndex { get; set; }

        public JobBoard(MissionManager missionManager)
        {
            _missionManager = missionManager;
        }

        /// <summary>
        /// Refresh the job board with new random missions
        /// </summary>
        public void RefreshMissions(int count = 6, string factionId = null)
        {
            _availableMissions.Clear();
            if (_missionManager != null)
            {
                _availableMissions.AddRange(_missionManager.GenerateJobBoardMissions(count, factionId));
            }
            SelectedIndex = 0;
            Console.WriteLine($"[JOB BOARD] Refreshed with {count} missions");
        }

        /// <summary>
        /// Select and accept the currently highlighted mission
        /// </summary>
        public bool AcceptSelectedMission()
        {
            if (_missionManager == null || SelectedIndex < 0 || SelectedIndex >= _availableMissions.Count)
                return false;

            var mission = _availableMissions[SelectedIndex];
            mission.OfferedBy = "Job Board";
            bool accepted = _missionManager.AcceptMission(mission);

            if (accepted)
            {
                _availableMissions.RemoveAt(SelectedIndex);
                if (SelectedIndex >= _availableMissions.Count && _availableMissions.Count > 0)
                    SelectedIndex = _availableMissions.Count - 1;
            }

            return accepted;
        }

        /// <summary>
        /// Navigate selection up
        /// </summary>
        public void MoveSelectionUp()
        {
            if (_availableMissions.Count == 0) return;
            SelectedIndex--;
            if (SelectedIndex < 0) SelectedIndex = _availableMissions.Count - 1;
        }

        /// <summary>
        /// Navigate selection down
        /// </summary>
        public void MoveSelectionDown()
        {
            if (_availableMissions.Count == 0) return;
            SelectedIndex++;
            if (SelectedIndex >= _availableMissions.Count) SelectedIndex = 0;
        }
    }
}
