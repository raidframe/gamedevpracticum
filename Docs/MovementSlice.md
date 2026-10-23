# First playable movement slice

Open `Assets/Scenes/RepairPrototype.unity` in Unity 6000.3.27f1 and press Play.
Click inside the Game view if it does not have keyboard focus.

## Controls

- WASD: walk at 3 metres per second.
- Mouse: look; vertical rotation is limited to 80 degrees in either direction.
- Escape: release the cursor and stop walking/looking.
- Left click in the Game view: capture the cursor again.
- Switching away from the application releases the cursor; click to resume.

## Scene and implementation

The prototype is the enabled startup scene in Build Settings. The original SampleScene remains on disk.
The room is an 8 by 6 metre blockout with four 3 metre walls, a floor, a directional light and three plain URP materials. A stripe on the north wall provides a visual reference for movement and looking.

`Assets/Scripts/FirstPersonController.cs` uses a CharacterController capsule for collision, with its feet at the Player object's origin. The child camera is 1.6 metres above the feet. Gravity is applied explicitly because CharacterController does not apply it automatically. Movement is relative to the player's horizontal facing direction. Input magnitude is clamped so diagonal walking is not faster.

The script creates and disposes its own two Input System actions. The existing template action asset remains untouched. Move speed, mouse sensitivity, pitch limit and gravity are editable on the Player's controller component. Change these outside Play mode to keep the values.

## Manual acceptance checklist

1. Enter Play mode and click the Game view. Verify WASD moves in all four directions and releasing keys stops horizontal motion.
2. Compare W with W+D: diagonal movement should not be faster.
3. Turn with the mouse, then press W: movement should follow the new horizontal facing direction. Looking up or down must not make the player fly.
4. Walk into every wall and corner. The capsule should stop or slide along the wall, never pass through it.
5. Stand still for 30 seconds: the player should remain on the floor without bouncing or sinking.
6. Look fully up and down: rotation should stop before flipping over.
7. Press Escape: the pointer should appear and WASD/mouse look should stop. Click the Game view to resume. Also check switching to another application and back.
8. Stop and restart Play mode: the player should return to the original spawn and controls should still work.
9. Check the Console for new errors after the above steps.

## Verification performed

- Unity imported and compiled the controller and generated the scene using its own editor APIs.
- A temporary editor check confirmed the controller, assigned camera, absence of missing scripts, and raycast hits against the floor and all four boundary walls.
- The scene was opened in Play mode and visually inspected; mouse look responded.
- The Unity Console showed zero errors and zero warnings after stopping Play mode and removing the temporary scene-generation helper.
- Full sustained keyboard movement, diagonal-speed comparison and corner collision acceptance checks remain manual; UI key taps did not provide a reliable movement test.

## Scope

Keyboard and mouse only. No jump, sprint, crouch, gamepad support, interaction, workbench or repair mechanics yet. Cursor release pauses movement input, not the game simulation. The room is deliberately a placeholder.

Suggested commit message: `Add first-person movement and repair kiosk prototype scene`.
