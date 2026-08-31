export const scrumPokerAvatarImages = Array.from(
  { length: 18 },
  (_, index) => ({
    value: `avatar-image-${index + 1}`,
    index,
  }),
);

export const scrumPokerAvatarSheetUrl = "/avatar-sprite.png";

export function scrumPokerAvatarImageStyle(
  avatar: (typeof scrumPokerAvatarImages)[number],
  includeVerticalOffset = true,
) {
  const column = avatar.index % 6;
  const row = Math.floor(avatar.index / 6);
  const verticalOffset = includeVerticalOffset ? "3px" : "0px";
  return {
    backgroundImage: `url("${scrumPokerAvatarSheetUrl}")`,
    backgroundPosition: `${column * 20}% calc(${row * 50}% + ${verticalOffset})`,
    backgroundSize: "600% 300%",
    backgroundOrigin: "border-box",
    backgroundRepeat: "no-repeat",
  };
}

export function scrumPokerAvatarForSelection(
  selection: string | null | undefined,
) {
  const image = scrumPokerAvatarImages.find(
    (avatar) => avatar.value === selection,
  );
  return image;
}
