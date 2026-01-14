# Design Documentation

This document outlines the design process and decisions made for the game project.

---

# User Experience (UX) Design

## Information Architecture

Users: Players and Game hosts

Context: Multiplatform app for an online version of a card game with hidden identities 

Content: Game enviroment, Game state, Game rules, Game mechanics 

Knowledge that the user may need at every stage of the game was grouped into categories based on their importance at the game phase, what is shown on the image below.

![Information Architecture](Information-architecture.png)

We identified interconnected groups of information:
- Personal Information Reminder: Details about the player's assigned role, abilities, informations and fraction.
- Gameplay Mechanics + Roles Reminder: Information about game rules, phases, and actions available to other players.
- The Game Theme: Will be kept by the user interface style.

Based on the information architecture we recognised following screens:
- Welcome Page
- Game Lobby screen
- Game Creation screen
- Game Managment screen
- Introduction screen
- Personal Information screen
- Game screen
- Personal Information Reminder (modal)
- Roles Reminder (modal)
- Notification History (modal)
- Team Choosing screen
- Voting screen
- Mission Resolution screen
- Game Summary screen

## User Flows

Logic and decision paths users take to complete tasks within the app.

| Image                                                         | User Flow Name       | Description |
|---------------------------------------------------------------|----------------------|-------------|
| ![Start a game user flow](user-flows/Start-a-game.png)        | Start a Game         | Initial setup flow where the host creates a new game, configures settings, and launches the session. |
| ![Join a game user flow](user-flows/Join-a-game.png)          | Join a new Game      | Flow allowing a player to join an existing game session by entering a game code or selecting an available lobby. |
| ![Reenter a game user flow](user-flows/Reenter-a-game.png)    | Enter a Game         | Flow used when a player reconnects to an ongoing game after disconnecting or refreshing the application. |
| ![Selection phase user flow](user-flows/Selection-phase.png)  | Get a role           | Flow in which players recive a roles and related information. |
| ![Remind Personal Informations user flow](user-flows/Remind-your-role.png)| Remind Your Game Role| Individual flow enabling a player to privately review their assigned role and related knowladge during the game. |
| ![Remind roles user flow](user-flows/Reminding-roles.png)     | Reminding Roles      | Individual flow enabling a player to review available roles during the game. |
| ![Choose a team user flow](user-flows/Team-choosing.png)      | Team Choosing        | Flow in which players are selected or assigned to a team for a mission or negotiation round. |
| ![Cast a vote user flow](user-flows/Voting.png)               | Voting               | Flow allowing players to cast votes on decisions such as team approval, mission outcomes, or negotiations. |
| ![Being on a mission user flow](user-flows/Mission.png)       | Mission              | Flow allowing players to cast votes after being allowed to be part of a negotiations. |
| ![Game completion user flow](user-flows/Game-completion.png)  | Replay a game        | Flow describing game summary with a replay option. |


## Screen Map

The structure of navigation through the app's screens based on user flows.

![Game completion user flow](Screen-map.png)

## Wireframes

Wireframe designs illustrating the layout and functionality of screens in the mobile version of the app.

![Wireframes](Wireframes.png)

---

TODO:

# User Interface (UI) Design

## Design System

### Color Palette

Color palette was prepared for both light and dark mode. 
Used colors with key contrast values are shown below:

![Color Palette Image](color-palette-light.png)
![Color Palette Dark Image](color-palette-dark.png)

### Typography

[Font Samples Image]

## Logos and Branding

[Logo Variations Image]

## High-Fidelity Mockups

[Mockup Visualisation Image]

## Prototype

Figma Prototype: [Link to Figma Prototype](https://www.figma.com/)

Prototype user testing
