# Functional Requirements

1. User Management & Connectivity
	- Game Lobby: The system shall provide a game lobby where players can join or create game rooms.
	- Cross-Platform Play: Users on Windows shall be able to join lobbies created by users on Android.

2. Game Logic & Mechanics
	- Role assignments: The system shall randomly assign roles to players at the start of each game.
	- Team Selection: The "Leader" shall be able to select a subset of players for a mission based on the game’s player-count scaling.
	- Voting System: The system shall allow all players to vote on the proposed team selection.
	- Mission Resolution: The system shall allow team members to secretly choose "Success"/"Failure"/"Reverse" and calculate the outcome based on the mission rules.
	- Win Conditions: The system shall determine the winning team based on the number of successful and failed missions.

# Non-Functional Requirements

1. Performance
	- Real-time Updates: The system shall synchronize the game state across all connected devices using a real-time communication layer.
	- Battery Consumption: The Android application shall not consume more than 15% of battery per hour of active gameplay.

2. Usability & UI/UX
	- Intuitive Interface: The user interface shall be designed to be intuitive and easy to navigate for users of all experience levels.
	- Accessibility: The application shall include accessibility features such as adjustable font sizes and color contrast options.
	- Responsive Design: The UI shall adapt to different aspect ratios, ensuring buttons remain tappable on mobile (minimum 44x44 dp) and legible on desktop.
	- Dark/Light Mode: The app shall support the system's theme settings via MAUI AppThemeBinding.

3. Reliability & Security
	- Data Integrity: The system shall ensure that game state data is consistently maintained and accurately reflected across all clients.
	- Secure Communication: All data transmitted shall be encrypted to protect user information and game integrity.
	- Data Privacy: The system shall not store personally identifiable information (PII) beyond the duration of a single game session.