import httpx

NPCS = {
    "elder_mira": {
        "name": "Elder Mira", "role": "Village elder",
        "personality": "wise, warm, observant, and quietly brave",
        "lore": "The village of Brindlewood is surrounded by an ancient forest. A blue beacon "
                "has awakened in the northern ruins, and creatures have begun leaving the woods. "
                "Mira protects the village archive and knows fragments of an old prophecy."
    },
    "kael": {
        "name": "Kael", "role": "Wandering ranger",
        "personality": "witty, cautious, practical, and protective",
        "lore": "Kael patrols the forest paths. He found strange glowing tracks leading north "
                "and believes someone has disturbed the sealed observatory."
    },
    "innkeeper_sera": {
        "name": "Sera", "role": "Innkeeper",
        "personality": "friendly, energetic, curious, and a little nosy",
        "lore": "Sera runs the Lantern & Loaf inn. Travelers have reported whispers from the "
                "old well. She trades rumors for interesting stories."
    }
}

class DialogueEngine:
    def __init__(self, api_key="", base_url="https://api.openai.com/v1", model="gpt-4o-mini"):
        self.api_key, self.base_url, self.model = api_key, base_url.rstrip("/"), model
        self.history = {}

    @property
    def ai_enabled(self):
        return bool(self.api_key)

    def reset(self, session_id):
        for key in list(self.history):
            if key[0] == session_id:
                del self.history[key]

    async def reply(self, session_id, npc_id, player_name, message):
        npc = NPCS[npc_id]
        key = (session_id, npc_id)
        history = self.history.setdefault(key, [])
        if self.ai_enabled:
            system = (
                f"You are {npc['name']}, a character in a fantasy RPG. Role: {npc['role']}. "
                f"Personality: {npc['personality']}. World lore: {npc['lore']} "
                f"The player's name is {player_name}. Stay in character, be concise (2-5 sentences), "
                "never claim actions were completed in the game, and offer clues rather than "
                "revealing every mystery. If asked for a quest, suggest a clear next objective."
            )
            messages = [{"role": "system", "content": system}, *history[-10:],
                        {"role": "user", "content": message}]
            async with httpx.AsyncClient(timeout=35) as client:
                response = await client.post(
                    f"{self.base_url}/chat/completions",
                    headers={"Authorization": f"Bearer {self.api_key}"},
                    json={"model": self.model, "messages": messages, "temperature": 0.85,
                          "max_tokens": 180},
                )
                response.raise_for_status()
                answer = response.json()["choices"][0]["message"]["content"].strip()
        else:
            answer = self._fallback(npc_id, message)
        history.extend([{"role": "user", "content": message},
                        {"role": "assistant", "content": answer}])
        self.history[key] = history[-12:]
        return answer

    def _fallback(self, npc_id, message):
        text = message.lower()
        if any(w in text for w in ("quest", "mission", "help", "task")):
            return {
                "elder_mira": "There is a task only a brave traveler can take. Visit the northern ruins and find the source of the blue beacon. Return with any symbol you discover.",
                "kael": "Follow the silver marks beyond the old bridge. Stay on the path, and if the forest goes silent, turn back. Bring me proof of what you find.",
                "innkeeper_sera": "A merchant left a map here. Ask Kael about the northern trail, then come back and tell me what is hiding near the ruins."
            }[npc_id]
        if any(w in text for w in ("forest", "woods", "ruins", "beacon", "north")):
            return {
                "elder_mira": "The beacon is older than Brindlewood itself. The archive says it was lit only when the seal beneath the observatory weakened.",
                "kael": "I saw blue light between the trees last night. Three sets of tracks went in, but none came back. Something is wrong up there.",
                "innkeeper_sera": "People say the trees whisper near the northern trail. I say people should stop wandering there after sunset!"
            }[npc_id]
        if any(w in text for w in ("name", "who are you")):
            return f"I am {NPCS[npc_id]['name']}. {NPCS[npc_id]['role']} of Brindlewood. What brings you to our village, traveler?"
        if any(w in text for w in ("hello", "hi", "hey", "greetings")):
            return f"Well met, traveler. I am {NPCS[npc_id]['name']}. What would you like to know about Brindlewood?"
        return {
            "elder_mira": "There is more to this village than its quiet streets. Ask me about the old stories, or speak with Kael about the northern forest.",
            "kael": "Keep your eyes open out there. The forest has been acting strangely. Ask me about the tracks if you want to know more.",
            "innkeeper_sera": "Welcome to the Lantern & Loaf! Rumors are free, but a warm meal costs a coin. Have you heard about the old well?"
        }[npc_id]
