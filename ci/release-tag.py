import sys
from git import Repo, TagReference
from github import Github
from github import Auth


def get_next_version(current_version: str) -> str:
    version_parts = current_version.split(".")

    if version_kind == "major":
        version_parts[0] = str(int(version_parts[0]) + 1)
    elif version_kind == "minor":
        version_parts[1] = str(int(version_parts[1]) + 1)
    elif version_kind == "patch":
        version_parts[2] = str(int(version_parts[2]) + 1)
    return ".".join(version_parts)


def get_current_version(github_token: str) -> str:
    auth = Auth.Token(github_token)
    github = Github(auth=auth)
    repo = github.get_repo("ombrelin/plex-rich-presence")
    return repo.get_latest_release().name


github_token: str = sys.argv[1]
version_kind: str = sys.argv[2]

print("Issuing release...")
print("Version kind : " + version_kind)

current_version: str = get_current_version(github_token)
print("Current version : " + current_version)
new_version: str = get_next_version(current_version)
print("Next version : " + new_version)

repo: Repo = Repo(".")
new_tag: TagReference = repo.create_tag(
    new_version, message='Version "{0}"'.format(new_version)
)
_ = repo.remotes.origin.push(new_tag.name)
