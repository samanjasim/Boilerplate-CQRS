# Comments & Activity — User Manual

The Comments & Activity module lets you discuss any entity (a product, a workflow request, a user profile, etc.) and read a chronological activity log of what's happened to it.

## Adding a comment

1. Open the detail page of any entity that supports comments (workflow requests, products, etc.).
2. Scroll to the **Comments & Activity** section.
3. Type your comment in the text box. Markdown is supported — you can use `**bold**`, `*italic*`, `> quotes`, `` `code` ``, and bullet lists.
4. Click **Post**.

## @mentioning users

Type `@` followed by a name to mention another user in your comment. A dropdown appears with matching users; click one to mention them.

Mentioned users receive a notification. By default the notification is in-app; they can opt into email notifications in **Settings → Notification Preferences** under "Comment mentions".

## The activity timeline

The timeline interleaves comments with system activity for the entity:

- **Comments** — posted by users.
- **Activity entries** — automatic events (request approved, step transitioned, delegation started, etc.).

Both are ordered chronologically with the newest at the top.

## Editing and deleting your comments

- **Edit** — click the three-dot menu on your comment, then **Edit**. Timestamps show "edited" after an edit.
- **Delete** — same menu, **Delete**. The comment is removed immediately. Only the author or an admin can delete.

## Who can see comments

Comments are visible to anyone in your tenant who has the `Comments.View` permission. Today, there is no per-entity access control — comments on a workflow request are visible to any user with the permission, not just workflow participants. This may change in future versions.
